using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WindowsCompanion.Helpers;
using WindowsCompanion.Models;
using WindowsCompanion.Services;

namespace WindowsCompanion.ViewModels;

/// <summary>A card of the System page after the filter.</summary>
/// <param name="Title">Card title.</param>
/// <param name="Rows">Lines matching the filter.</param>
public sealed record SystemCard(string Title, IReadOnlyList<InfoRow> Rows);

/// <summary>System page: six cards, filter, export, summary (cahier 4.5).</summary>
public sealed partial class SystemViewModel : ObservableObject, IPageViewModel, IRecipient<HardwareSnapshotMessage>
{
    private readonly ISystemInfoService _system;
    private readonly IExportService _export;
    private readonly IClipboardService _clipboard;
    private readonly IFeedbackService _feedback;
    private readonly IMessenger _messenger;
    private readonly TimeProvider _time;
    private HardwareSnapshot? _hardware;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Cards), nameof(HasNoMatch))]
    private SystemSnapshot? _snapshot;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Cards), nameof(HasNoMatch))]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    /// <summary>Initializes a new instance of the <see cref="SystemViewModel"/> class.</summary>
    /// <param name="system">System reading.</param>
    /// <param name="export">Reports and summary.</param>
    /// <param name="clipboard">Clipboard.</param>
    /// <param name="feedback">Toasts.</param>
    /// <param name="messenger">Shared messages.</param>
    /// <param name="time">Clock.</param>
    public SystemViewModel(ISystemInfoService system, IExportService export, IClipboardService clipboard, IFeedbackService feedback, IMessenger messenger, TimeProvider time)
    {
        _system = system;
        _export = export;
        _clipboard = clipboard;
        _feedback = feedback;
        _messenger = messenger;
        _time = time;
        messenger.RegisterAll(this);
    }

    /// <inheritdoc />
    public PageKey Page => PageKey.System;

    /// <inheritdoc />
    public IAsyncRelayCommand? PrimaryCommand => null;

    /// <summary>Gets the cards that keep at least one line after the filter (F-SYS-06).</summary>
    public IReadOnlyList<SystemCard> Cards
    {
        get
        {
            if (Snapshot is null)
            {
                return [];
            }

            var filter = FilterText.Trim();
            return [.. Snapshot.Groups
                .Select(g => new SystemCard(InfoRows.GroupTitle(g.Kind), [.. g.Rows.Where(r => filter.Length == 0 || (r.Label + " " + InfoRows.ValueText(r)).Contains(filter, StringComparison.CurrentCultureIgnoreCase))]))
                .Where(c => c.Rows.Count > 0)];
        }
    }

    /// <summary>Gets a value indicating whether the filter hides every line (F-SYS-07).</summary>
    public bool HasNoMatch => Snapshot is not null && Cards.Count == 0;

    /// <summary>Reads the snapshot at startup, for the overview.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task completed once the snapshot is published.</returns>
    public Task InitializeAsync(CancellationToken ct) => LoadCommand.ExecuteAsync(null);

    /// <inheritdoc />
    public Task OnNavigatedToAsync(CancellationToken ct)
    {
        // Read again when the last reading is older than 5 minutes (F-SYS-03).
        var stale = Snapshot is null || _time.GetLocalNow() - Snapshot.TakenAt > TimeSpan.FromMinutes(AppConfig.SystemStaleMinutes);
        return stale && LoadCommand.CanExecute(null) ? LoadCommand.ExecuteAsync(null) : Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Receive(HardwareSnapshotMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        _hardware = message.Snapshot;
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken ct)
    {
        IsLoading = true;
        try
        {
            var snapshot = await _system.GetSnapshotAsync(ct);
            Snapshot = snapshot;
            _messenger.Send(new SystemSnapshotMessage(snapshot));
        }
        catch (OperationCanceledException)
        {
            // Not an error.
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ExportAsync(CancellationToken ct)
    {
        var result = await _export.ExportReportAsync(ExportKind.System, new ReportContent(null, Snapshot, null, null), ct);
        PageFeedback.ShowExport(_feedback, _export, result, () => ExportCommand.ExecuteAsync(null));
    }

    [RelayCommand]
    private void CopySummary() => PageFeedback.ShowCopied(_feedback, _clipboard, _export.BuildSummary(_hardware, Snapshot));
}
