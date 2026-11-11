using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using WindowsCompanion.Helpers;
using WindowsCompanion.Models;
using WindowsCompanion.Resources;
using WindowsCompanion.Services;

namespace WindowsCompanion.ViewModels;

/// <summary>Source filter of the update table.</summary>
public enum SourceFilter
{
    /// <summary>Every source.</summary>
    All,

    /// <summary>winget only.</summary>
    Winget,

    /// <summary>Microsoft Store only.</summary>
    MsStore,
}

/// <summary>Updates page: search, selection, installation queue, cancellation, ignore, winget log (cahier 4.3).</summary>
public sealed partial class UpdatesViewModel : ObservableObject, IPageViewModel,
    IRecipient<SearchUpdatesRequestedMessage>, IRecipient<SettingsChangedMessage>
{
    private readonly IWingetService _winget;
    private readonly IPreferencesService _preferences;
    private readonly IHistoryService _history;
    private readonly IRunningAppService _running;
    private readonly IFeedbackService _feedback;
    private readonly IClipboardService _clipboard;
    private readonly IPageNavigator _navigator;
    private readonly IMessenger _messenger;
    private readonly TimeProvider _time;
    private readonly ILogger<UpdatesViewModel> _logger;
    private readonly List<UpdateItemViewModel> _items = [];
    private readonly List<UpdateItemViewModel> _queue = [];
    private readonly InstallBatch _batch = new();
    private readonly Lock _logSync = new();
    private UpdateItemViewModel? _current;
    private CancellationTokenSource? _currentCts;
    private InstallProgress? _currentProgress;
    private bool _cancelAll;
    private int _succeeded;
    private int _failed;

    [ObservableProperty]
    private SourceFilter _sourceFilter;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private bool _hasSearched;

    [ObservableProperty]
    private bool _isWingetMissing;

    [ObservableProperty]
    private string? _wingetVersion;

    [ObservableProperty]
    private DateTimeOffset? _lastSearchAt;

    [ObservableProperty]
    private string _progressLabel = string.Empty;

    [ObservableProperty]
    private double _globalPercent;

    /// <summary>Initializes a new instance of the <see cref="UpdatesViewModel"/> class.</summary>
    /// <param name="winget">winget access.</param>
    /// <param name="preferences">Settings and ignored applications.</param>
    /// <param name="history">Installation history.</param>
    /// <param name="running">Detects and closes open applications.</param>
    /// <param name="feedback">Toasts.</param>
    /// <param name="clipboard">Clipboard.</param>
    /// <param name="navigator">Page navigation.</param>
    /// <param name="messenger">Shared messages.</param>
    /// <param name="time">Clock.</param>
    /// <param name="logger">Logger.</param>
    public UpdatesViewModel(
        IWingetService winget,
        IPreferencesService preferences,
        IHistoryService history,
        IRunningAppService running,
        IFeedbackService feedback,
        IClipboardService clipboard,
        IPageNavigator navigator,
        IMessenger messenger,
        TimeProvider time,
        ILogger<UpdatesViewModel> logger)
    {
        _winget = winget;
        _preferences = preferences;
        _history = history;
        _running = running;
        _feedback = feedback;
        _clipboard = clipboard;
        _navigator = navigator;
        _messenger = messenger;
        _time = time;
        _logger = logger;
        messenger.RegisterAll(this);
    }

    /// <inheritdoc />
    public PageKey Page => PageKey.Updates;

    /// <inheritdoc />
    public IAsyncRelayCommand PrimaryCommand => SearchCommand;

    /// <summary>Gets the rows shown after the source filter and the title bar search.</summary>
    public ObservableCollection<UpdateItemViewModel> VisibleItems { get; } = [];

    /// <summary>Gets the winget log lines (2 000 at most, F-MAJ-43).</summary>
    public ObservableCollection<WingetLogLine> Log { get; } = [];

    /// <summary>Gets the number of updates neither installed nor waiting for an application to close (F-NAV-04).</summary>
    public int PendingCount => Pending().Count();

    /// <summary>Gets a value indicating whether the navigation badge is shown.</summary>
    public bool HasPending => PendingCount > 0;

    /// <summary>Gets the status bar text about winget (F-FEN-10).</summary>
    public string WingetStatusText => IsWingetMissing
        ? Strings.Status_WingetMissing
        : WingetVersion is null ? Strings.Status_WingetChecking : Formats.Template(Strings.Status_WingetReady, WingetVersion);

    /// <summary>Gets the status bar text about the last search (F-FEN-11).</summary>
    public string LastSearchText => LastSearchAt is { } at
        ? Formats.Template(Strings.Status_LastSearch, Formats.Relative(at, _time.GetLocalNow(), capitalize: false))
        : Strings.Status_NoSearch;

    /// <summary>Gets the page subtitle: active sources.</summary>
    public string SourcesText => Formats.Template(Strings.Upd_Subtitle, string.Join(", ", ActiveSources().Select(InfoRows.SourceLongLabel)));

    /// <summary>Gets the summary title.</summary>
    public string SummaryTitle => Formats.Plural(PendingCount, Strings.Upd_SummaryOne, Strings.Upd_SummaryMany);

    /// <summary>Gets the summary size line.</summary>
    public string SummarySize => Summary() is { KnownBytes: > 0 } s
        ? Formats.Template(Strings.Upd_ToDownload, Formats.DownloadTotal(s.KnownBytes, s.HasUnknownSize))
        : string.Empty;

    /// <summary>Gets the sources searched, shown while searching.</summary>
    public string SearchingSourcesText => Formats.JoinWithAnd([.. ActiveSources().Select(InfoRows.SourceLongLabel)]);

    /// <summary>Gets the state of the header check box: true, false, or null when some rows are checked (F-MAJ-15).</summary>
    public bool? HeaderSelection
    {
        get
        {
            var selectable = VisibleItems.Where(i => i.IsSelectable).ToList();
            var selected = selectable.Count(i => i.IsSelected);
            return selected == 0 ? false : selected == selectable.Count ? true : null;
        }
    }

    /// <summary>Gets a value indicating whether a visible row can be checked.</summary>
    public bool CanSelectAny => VisibleItems.Any(i => i.IsSelectable);

    /// <summary>Gets the label of the "install selection" button, with the count when rows are checked.</summary>
    public string InstallSelectionLabel => SelectedVisible().Count is var n and > 0
        ? Formats.Template(Strings.Upd_InstallSelectionCount, n)
        : Strings.Upd_InstallSelection;

    /// <summary>Gets a value indicating whether no row is visible.</summary>
    public bool IsEmpty => VisibleItems.Count == 0 && !IsSearching;

    /// <summary>Gets the title of the empty state.</summary>
    public string EmptyTitle => EmptyTexts().Title;

    /// <summary>Gets the text of the empty state.</summary>
    public string EmptyText => EmptyTexts().Text;

    /// <summary>Gets a value indicating whether the empty state is the "up to date" one (green icon).</summary>
    public bool IsUpToDate => !IsWingetMissing && HasSearched && _items.Count == 0;

    /// <summary>Gets the tooltip of the search button: explains why it is disabled when no source is active (F-MAJ-39).</summary>
    public string? SearchToolTip => ActiveSources().Count == 0 ? Strings.Upd_NoSourceTooltip : null;

    /// <summary>Gets the running installation loop, for the tests and the exit sequence.</summary>
    internal Task QueueTask { get; private set; } = Task.CompletedTask;

    /// <summary>Reads the winget version and the time of the last search, at startup.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task completed once the version is known.</returns>
    public async Task InitializeAsync(CancellationToken ct)
    {
        LastSearchAt = _preferences.Current.LastSearchAt;
        var version = await _winget.GetVersionAsync(ct);
        IsWingetMissing = !version.Ok;
        WingetVersion = version.Data;
        RefreshAll();
    }

    /// <inheritdoc />
    public Task OnNavigatedToAsync(CancellationToken ct)
    {
        OnPropertyChanged(nameof(LastSearchText));
        return Task.CompletedTask;
    }

    /// <summary>Cancels everything without toast and waits for the loop to record the history (exit, F-LIM-04).</summary>
    /// <returns>A task completed once the loop has ended.</returns>
    public Task CancelAllAndWaitAsync()
    {
        CancelEverything(showToast: false);
        return QueueTask;
    }

    /// <inheritdoc />
    public void Receive(SearchUpdatesRequestedMessage message)
    {
        if (SearchCommand.CanExecute(null))
        {
            SearchCommand.Execute(null);
        }
    }

    /// <inheritdoc />
    public void Receive(SettingsChangedMessage message) => RefreshAll();

    partial void OnSourceFilterChanged(SourceFilter value) => Refilter();

    partial void OnIsSearchingChanged(bool value) => RefreshAll();

    partial void OnIsWingetMissingChanged(bool value) => RefreshAll();

    partial void OnWingetVersionChanged(string? value) => OnPropertyChanged(nameof(WingetStatusText));

    partial void OnLastSearchAtChanged(DateTimeOffset? value) => OnPropertyChanged(nameof(LastSearchText));

    private bool CanSearch() => !IsSearching && !IsInstalling && !IsWingetMissing && ActiveSources().Count > 0;

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private async Task SearchAsync(CancellationToken ct)
    {
        IsSearching = true;
        try
        {
            var settings = _preferences.Current;
            IProgress<WingetLogLine> log = new Progress<WingetLogLine>(AppendLog);
            var result = await _winget.SearchUpgradesAsync(ActiveSources(), settings.IncludeUnknownVersions, log.Report, ct);
            if (!result.Ok)
            {
                _feedback.Show(result.Error!, RetrySearch());
                return;
            }

            var excluded = settings.Exclusions.Select(e => e.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            _items.Clear();
            _items.AddRange(result.Data!.Items.Where(i => !excluded.Contains(i.Id)).Select(i => new UpdateItemViewModel(i, OnSelectionChanged)));
            HasSearched = true;
            LastSearchAt = _time.GetLocalNow();
            await _preferences.UpdateAsync(s => s with { LastSearchAt = LastSearchAt }, ct);
            Refilter();
            SendSummary();

            foreach (var source in result.Data.FailedSources)
            {
                _feedback.Show(new(FeedbackType.Warning, Strings.Toast_SourceUnavailable_Title, Formats.Template(Strings.Toast_SourceUnavailable_Message, InfoRows.SourceLongLabel(source))), RetrySearch());
            }

            if (_items.Count > 0)
            {
                _feedback.Show(
                    new(FeedbackType.Info, Strings.Toast_SearchDone_Title, Formats.Template(
                        _items.Count <= 1 ? Strings.Toast_SearchDone_MessageOne : Strings.Toast_SearchDone_MessageMany,
                        Formats.Whole(_items.Count),
                        SearchingSourcesText)),
                    new ToastAction(Strings.Upd_InstallAll, true, () =>
                    {
                        InstallAll();
                        return Task.CompletedTask;
                    }));
            }
            else
            {
                _feedback.Show(new(FeedbackType.Success, Strings.Toast_UpToDate_Title, Strings.Toast_UpToDate_Message));
            }
        }
        catch (OperationCanceledException)
        {
            // Not an error (rules/errors.md).
        }
        finally
        {
            IsSearching = false;
        }
    }

    private bool CanInstallSelection() => !IsSearching && SelectedVisible().Count > 0;

    [RelayCommand(CanExecute = nameof(CanInstallSelection))]
    private void InstallSelection() => Enqueue(SelectedVisible());

    private bool CanInstallAll() => !IsSearching && _items.Exists(i => i.IsSelectable);

    [RelayCommand(CanExecute = nameof(CanInstallAll))]
    private void InstallAll() => Enqueue([.. _items.Where(i => i.IsSelectable)]);

    [RelayCommand]
    private void InstallItem(UpdateItemViewModel? item)
    {
        if (item is not null && !IsSearching)
        {
            Enqueue([item]);
        }
    }

    [RelayCommand]
    private void CancelItem(UpdateItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        if (item == _current)
        {
            _currentCts?.Cancel();
            return;
        }

        if (_queue.Remove(item))
        {
            item.State = UpdateState.Cancelled;
            _batch.RemoveWaiting();
            UpdateProgress();
            _feedback.Show(new(FeedbackType.Info, Strings.Toast_Dequeued_Title, Formats.Template(Strings.Toast_Dequeued_Message, item.Name)));
            RefreshAll();
        }
    }

    private bool CanCancelAll() => IsInstalling;

    [RelayCommand(CanExecute = nameof(CanCancelAll))]
    private void CancelAll() => CancelEverything(showToast: true);

    [RelayCommand]
    private async Task CloseAppAsync(UpdateItemViewModel? item)
    {
        if (item is not { State: UpdateState.AppOpen })
        {
            return;
        }

        if (await _running.CloseAsync(item.Model, CancellationToken.None))
        {
            item.State = UpdateState.Installed;
            _feedback.Show(new(FeedbackType.Success, Formats.Template(Strings.Toast_AppUpdated_Title, item.Name), Formats.Template(Strings.Toast_AppUpdated_Message, item.AvailableVersion)));
            SendSummary();
        }
        else
        {
            ShowAppOpenToast(item);
        }
    }

    private bool CanIgnoreSelection() => SelectedVisible().Count > 0;

    [RelayCommand(CanExecute = nameof(CanIgnoreSelection))]
    private async Task IgnoreSelectionAsync(CancellationToken ct)
    {
        var ignored = SelectedVisible();
        var now = _time.GetLocalNow();
        foreach (var item in ignored)
        {
            item.IsSelected = false;
            _items.Remove(item);
        }

        await _preferences.AddExclusionsAsync(ignored.Select(i => new ExclusionEntry(i.Id, i.Name, Strings.Upd_IgnoredReason, now)), ct);
        Refilter();
        SendSummary();
        _messenger.Send(new SettingsChangedMessage());

        _feedback.Show(
            new(FeedbackType.Info, Formats.Plural(ignored.Count, Strings.Toast_Ignored_TitleOne, Strings.Toast_Ignored_TitleMany), ignored.Count == 1 ? Strings.Toast_Ignored_MessageOne : Strings.Toast_Ignored_MessageMany),
            new ToastAction(Strings.Toast_Undo, true, async () =>
            {
                _items.AddRange(ignored);
                await _preferences.RemoveExclusionsAsync(ignored.Select(i => i.Id), CancellationToken.None);
                Refilter();
                SendSummary();
                _messenger.Send(new SettingsChangedMessage());
            }));
    }

    [RelayCommand]
    private void ToggleAll()
    {
        var target = HeaderSelection != true;
        foreach (var item in VisibleItems.Where(i => i.IsSelectable))
        {
            item.IsSelected = target;
        }
    }

    [RelayCommand]
    private void ClearLog()
    {
        lock (_logSync)
        {
            Log.Clear();
        }
    }

    private List<UpdateSource> ActiveSources()
    {
        var settings = _preferences.Current;
        var sources = new List<UpdateSource>();
        if (settings.UseWinget)
        {
            sources.Add(UpdateSource.Winget);
        }

        if (settings.UseMsStore)
        {
            sources.Add(UpdateSource.MsStore);
        }

        return sources;
    }

    // D9: only the checked rows that are visible count and are processed.
    private List<UpdateItemViewModel> SelectedVisible() => [.. VisibleItems.Where(i => i.IsSelected && i.IsSelectable)];

    private IEnumerable<UpdateItemViewModel> Pending() => _items.Where(i => i.State is not (UpdateState.Installed or UpdateState.AppOpen));

    private UpdatesSummary Summary()
    {
        if (!HasSearched)
        {
            return UpdatesSummary.NotSearched;
        }

        var pending = Pending().ToList();
        var largest = pending.Where(i => i.Model.SizeBytes is not null).MaxBy(i => i.Model.SizeBytes);
        return new UpdatesSummary(
            true,
            pending.Count,
            pending.Sum(i => i.Model.SizeBytes ?? 0),
            pending.Exists(i => i.Model.SizeBytes is null),
            largest?.Name,
            largest?.Model.SizeBytes ?? 0);
    }

    private (string Title, string Text) EmptyTexts() => this switch
    {
        { IsWingetMissing: true } => (Strings.Upd_WingetMissingTitle, Strings.Upd_WingetMissingText),
        { HasSearched: false } => (Strings.Upd_NotSearchedTitle, Strings.Upd_NotSearchedText),
        _ when _items.Count == 0 => (Strings.Upd_UpToDateTitle, Strings.Upd_UpToDateText),
        _ => (Strings.Upd_NoResultTitle, Strings.Upd_NoResultText),
    };

    private ToastAction RetrySearch() => new(Strings.Toast_Retry, true, () => SearchCommand.CanExecute(null) ? SearchCommand.ExecuteAsync(null) : Task.CompletedTask);

    private void OnSelectionChanged() => RefreshAll();

    private void Refilter()
    {
        var visible = _items
            .Where(i => SourceFilter switch
            {
                SourceFilter.Winget => i.Model.Source == UpdateSource.Winget,
                SourceFilter.MsStore => i.Model.Source == UpdateSource.MsStore,
                _ => true,
            })
            .ToList();

        VisibleItems.Clear();
        foreach (var item in visible)
        {
            VisibleItems.Add(item);
        }

        RefreshAll();
    }

    private void RefreshAll()
    {
        OnPropertyChanged(nameof(PendingCount));
        OnPropertyChanged(nameof(HasPending));
        OnPropertyChanged(nameof(SummaryTitle));
        OnPropertyChanged(nameof(SummarySize));
        OnPropertyChanged(nameof(HeaderSelection));
        OnPropertyChanged(nameof(CanSelectAny));
        OnPropertyChanged(nameof(InstallSelectionLabel));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptyText));
        OnPropertyChanged(nameof(IsUpToDate));
        OnPropertyChanged(nameof(SourcesText));
        OnPropertyChanged(nameof(SearchingSourcesText));
        OnPropertyChanged(nameof(SearchToolTip));
        OnPropertyChanged(nameof(WingetStatusText));
        SearchCommand.NotifyCanExecuteChanged();
        InstallSelectionCommand.NotifyCanExecuteChanged();
        InstallAllCommand.NotifyCanExecuteChanged();
        CancelAllCommand.NotifyCanExecuteChanged();
        IgnoreSelectionCommand.NotifyCanExecuteChanged();
    }

    private void SendSummary()
    {
        RefreshAll();
        _messenger.Send(new UpdatesChangedMessage(Summary(), [.. Pending().Select(i => i.Model)]));
    }

    private void AppendLog(WingetLogLine line)
    {
        lock (_logSync)
        {
            Log.Add(line);
            while (Log.Count > AppConfig.WingetLogMaxLines)
            {
                Log.RemoveAt(0);
            }
        }
    }

    private void Enqueue(IReadOnlyList<UpdateItemViewModel> items)
    {
        var added = items.Where(i => i.IsSelectable && !_queue.Contains(i) && i != _current).ToList();
        if (added.Count == 0)
        {
            return;
        }

        foreach (var item in added)
        {
            item.State = UpdateState.Queued;
            _queue.Add(item);
        }

        _batch.Add(added.Count);
        if (!IsInstalling)
        {
            IsInstalling = true;
            QueueTask = ProcessQueueAsync();
        }

        UpdateProgress();
        RefreshAll();
    }

    private void CancelEverything(bool showToast)
    {
        var count = _queue.Count + (_current is null ? 0 : 1);
        foreach (var item in _queue)
        {
            item.State = UpdateState.Cancelled;
        }

        _queue.Clear();
        if (_current is not null)
        {
            _cancelAll = true;
            _currentCts?.Cancel();
        }

        if (showToast && count > 0)
        {
            _feedback.Show(new(FeedbackType.Info, Strings.Toast_AllCancelled_Title, Formats.Plural(count, Strings.Toast_AllCancelled_MessageOne, Strings.Toast_AllCancelled_MessageMany)));
        }

        RefreshAll();
    }

    // Fire-and-forget loop started by the install commands: faults are logged here, never lost (rules/threading.md).
    private async Task ProcessQueueAsync()
    {
        try
        {
            while (_queue.Count > 0)
            {
                var item = _queue[0];
                _queue.RemoveAt(0);
                await ProcessItemAsync(item);
                _batch.MarkCompleted();
                UpdateProgress();
            }

            if (_succeeded > 0 && _failed == 0)
            {
                _feedback.Show(
                    new(FeedbackType.Success, Strings.Toast_BatchDone_Title, Formats.Plural(_succeeded, Strings.Toast_BatchDone_MessageOne, Strings.Toast_BatchDone_MessageMany)),
                    new ToastAction(Strings.Toast_SeeHistory, true, () =>
                    {
                        _navigator.Navigate(PageKey.History);
                        return Task.CompletedTask;
                    }));
            }
            else if (_succeeded > 0)
            {
                _feedback.Show(new(FeedbackType.Warning, Strings.Toast_BatchErrors_Title, Formats.Template(Strings.Toast_BatchErrors_Message, _succeeded, _failed)));
            }
        }
        catch (Exception ex)
        {
            LogQueueFailed(_logger, ex);
        }
        finally
        {
            _batch.Reset();
            _succeeded = 0;
            _failed = 0;
            _cancelAll = false;
            _current = null;
            IsInstalling = false;
            UpdateProgress();
            SendSummary();
        }
    }

    private async Task ProcessItemAsync(UpdateItemViewModel item)
    {
        _current = item;
        using var cts = new CancellationTokenSource();
        _currentCts = cts;
        item.StartedAt = _time.GetUtcNow();
        item.ProgressPercent = 0;
        item.State = UpdateState.Downloading;
        _currentProgress = new InstallProgress(InstallPhase.Downloading, 0);
        UpdateProgress();

        var progress = new Progress<InstallProgress>(p =>
        {
            if (item.State is not (UpdateState.Downloading or UpdateState.Installing))
            {
                return; // late report after the end
            }

            _currentProgress = p;
            if (p.Phase == InstallPhase.Installing)
            {
                item.State = UpdateState.Installing;
            }
            else
            {
                item.ProgressPercent = p.Percent;
            }

            UpdateProgress();
        });
        IProgress<WingetLogLine> log = new Progress<WingetLogLine>(AppendLog);

        var outcome = await _winget.InstallAsync(item.Model, _preferences.Current.SilentInstall, progress, log.Report, cts.Token);
        var phase = item.State;
        var duration = (int)Math.Round((_time.GetUtcNow() - (item.StartedAt ?? _time.GetUtcNow())).TotalSeconds);

        switch (outcome.Kind)
        {
            case InstallResultKind.Installed:
                var open = await _running.IsRunningAsync(item.Model, CancellationToken.None);
                item.State = open ? UpdateState.AppOpen : UpdateState.Installed;
                _succeeded++;
                if (open)
                {
                    ShowAppOpenToast(item);
                }

                await AddHistoryAsync(item, HistoryResult.Success, null, duration);
                break;

            case InstallResultKind.Failed:
                item.ExitCode = outcome.ExitCode;
                item.State = UpdateState.Failed;
                _failed++;
                ShowFailedToast(item);
                await AddHistoryAsync(item, HistoryResult.Failure, outcome.ExitCode, duration);
                break;

            default:
                item.State = UpdateState.Cancelled;
                if (!_cancelAll)
                {
                    _feedback.Show(phase == UpdateState.Installing
                        ? new(FeedbackType.Warning, Strings.Toast_InstallInterrupted_Title, Formats.Template(Strings.Toast_InstallInterrupted_Message, item.Name))
                        : new(FeedbackType.Info, Strings.Toast_DownloadCancelled_Title, Formats.Template(Strings.Toast_DownloadCancelled_Message, item.Name)));
                }

                await AddHistoryAsync(item, HistoryResult.Cancelled, null, null);
                break;
        }

        _currentCts = null;
        _current = null;
        _currentProgress = null;
        SendSummary();
    }

    private async Task AddHistoryAsync(UpdateItemViewModel item, HistoryResult result, string? exitCode, int? duration)
    {
        await _history.AddAsync(
            new HistoryEntry(_time.GetLocalNow(), item.Id, item.Name, item.Model.InstalledVersion, item.AvailableVersion, item.Model.Source, result, exitCode, duration),
            CancellationToken.None);
        _messenger.Send(new HistoryChangedMessage());
    }

    private void ShowFailedToast(UpdateItemViewModel item) => _feedback.Show(
        new(FeedbackType.Danger, Formats.Template(Strings.Toast_InstallFailed_Title, item.Name), Formats.Template(Strings.Toast_InstallFailed_Message, item.ExitCode ?? string.Empty)),
        new ToastAction(Strings.Toast_Retry, true, () =>
        {
            InstallItem(item);
            return Task.CompletedTask;
        }),
        new ToastAction(Strings.Toast_CopyDetail, false, () =>
        {
            _clipboard.SetText(Formats.Template(Strings.Toast_InstallFailed_Detail, item.Id, item.ExitCode ?? string.Empty));
            return Task.CompletedTask;
        }));

    private void ShowAppOpenToast(UpdateItemViewModel item) => _feedback.Show(
        new(FeedbackType.Warning, Formats.Template(Strings.Toast_AppOpen_Title, item.Name), Strings.Toast_AppOpen_Message),
        new ToastAction(Formats.Template(Strings.Toast_CloseApp, item.Name), true, () => CloseAppCommand.ExecuteAsync(item)),
        new ToastAction(Strings.Toast_Later, false, () => Task.CompletedTask));

    private void UpdateProgress()
    {
        GlobalPercent = _batch.GlobalPercent(_currentProgress);
        ProgressLabel = _current is null
            ? string.Empty
            : Formats.Template(Strings.Upd_ProgressLabel, _batch.CurrentPosition, _batch.Total, _current.Name);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Echec de la file d'installation")]
    private static partial void LogQueueFailed(ILogger logger, Exception exception);
}
