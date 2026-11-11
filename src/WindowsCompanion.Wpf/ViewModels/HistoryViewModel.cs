using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WindowsCompanion.Helpers;
using WindowsCompanion.Models;
using WindowsCompanion.Resources;
using WindowsCompanion.Services;

namespace WindowsCompanion.ViewModels;

/// <summary>A row of the history table.</summary>
/// <param name="Entry">The entry.</param>
/// <param name="DateText">Relative date (F-HIS-06).</param>
/// <param name="VersionText">"from to target".</param>
/// <param name="ResultText">Chip text.</param>
/// <param name="DurationText">Duration, "-" for a cancellation.</param>
public sealed record HistoryRow(HistoryEntry Entry, string DateText, string VersionText, string ResultText, string DurationText);

/// <summary>History page: table, CSV export, two-step clearing (cahier 4.7).</summary>
public sealed partial class HistoryViewModel : ObservableObject, IPageViewModel, IRecipient<HistoryChangedMessage>
{
    private readonly IHistoryService _history;
    private readonly IExportService _export;
    private readonly IFeedbackService _feedback;
    private readonly TimeProvider _time;
    private int _armVersion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ClearLabel))]
    private bool _isClearArmed;

    [ObservableProperty]
    private bool _isEmpty = true;

    /// <summary>Initializes a new instance of the <see cref="HistoryViewModel"/> class.</summary>
    /// <param name="history">History storage.</param>
    /// <param name="export">CSV export.</param>
    /// <param name="feedback">Toasts.</param>
    /// <param name="messenger">Shared messages.</param>
    /// <param name="time">Clock.</param>
    public HistoryViewModel(IHistoryService history, IExportService export, IFeedbackService feedback, IMessenger messenger, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(messenger);
        _history = history;
        _export = export;
        _feedback = feedback;
        _time = time;
        messenger.RegisterAll(this);
    }

    /// <inheritdoc />
    public PageKey Page => PageKey.History;

    /// <inheritdoc />
    public IAsyncRelayCommand? PrimaryCommand => null;

    /// <summary>Gets the rows, most recent first.</summary>
    public ObservableCollection<HistoryRow> Entries { get; } = [];

    /// <summary>Gets the label of the clear button: "Confirmer l'effacement" once armed (F-HIS-07).</summary>
    public string ClearLabel => IsClearArmed ? Strings.His_ConfirmClear : Strings.His_Clear;

    /// <summary>Reads the history at startup.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task completed once the rows are shown.</returns>
    public Task InitializeAsync(CancellationToken ct) => LoadCommand.ExecuteAsync(null);

    /// <inheritdoc />
    public Task OnNavigatedToAsync(CancellationToken ct)
    {
        Refresh(Entries.Select(r => r.Entry).ToList());
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Receive(HistoryChangedMessage message)
    {
        if (LoadCommand.CanExecute(null))
        {
            LoadCommand.Execute(null);
        }
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken ct)
    {
        var load = await _history.ListAsync(ct);
        Refresh(load.Entries);
        if (load.WasReset)
        {
            _feedback.Show(new(FeedbackType.Warning, Strings.Toast_Reset_Title, Strings.Toast_Reset_Message));
        }
    }

    [RelayCommand]
    private async Task ExportCsvAsync(CancellationToken ct)
    {
        var result = await _export.ExportHistoryCsvAsync([.. Entries.Select(r => r.Entry)], ct);
        PageFeedback.ShowExport(_feedback, _export, result, () => ExportCsvCommand.ExecuteAsync(null));
    }

    // First press arms for 3 s, second press clears; both presses run while the first one waits.
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ClearAsync(CancellationToken ct)
    {
        if (IsClearArmed)
        {
            IsClearArmed = false;
            _armVersion++;
            await _history.ClearAsync(ct);
            Refresh([]);
            _feedback.Show(new(FeedbackType.Info, Strings.Toast_HistoryCleared_Title, Strings.Toast_HistoryCleared_Message));
            return;
        }

        IsClearArmed = true;
        var version = ++_armVersion;
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(AppConfig.ClearConfirmMs), _time, ct);
        }
        catch (OperationCanceledException)
        {
            // The second press cancels this wait (the command cancels its previous run): not an error (rules/errors.md).
        }

        if (version == _armVersion)
        {
            IsClearArmed = false;
        }
    }

    private void Refresh(IReadOnlyList<HistoryEntry> entries)
    {
        var now = _time.GetLocalNow();
        Entries.Clear();
        foreach (var e in entries)
        {
            Entries.Add(new HistoryRow(
                e,
                Formats.Relative(e.Timestamp, now, capitalize: true),
                Formats.Template(Strings.His_VersionLine, e.FromVersion ?? Strings.Upd_VersionUnknown, e.ToVersion),
                InfoRows.HistoryResultLabel(e.Result),
                e.DurationSeconds is { } d ? Formats.Duration(d) : Strings.Common_Dash));
        }

        IsEmpty = Entries.Count == 0;
    }
}
