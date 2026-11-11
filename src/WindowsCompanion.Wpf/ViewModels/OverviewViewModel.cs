using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WindowsCompanion.Helpers;
using WindowsCompanion.Models;
using WindowsCompanion.Resources;
using WindowsCompanion.Services;

namespace WindowsCompanion.ViewModels;

/// <summary>Overview page: four tiles, recommended actions, storage card (cahier 4.2).</summary>
public sealed partial class OverviewViewModel : ObservableObject, IPageViewModel,
    IRecipient<UpdatesChangedMessage>, IRecipient<HardwareSnapshotMessage>, IRecipient<SystemSnapshotMessage>, IRecipient<NetworkSnapshotMessage>
{
    private readonly IRecommendationService _recommendations;
    private readonly IExportService _export;
    private readonly IFeedbackService _feedback;
    private readonly IPageNavigator _navigator;
    private readonly IMessenger _messenger;
    private readonly TimeProvider _time;
    private UpdatesSummary _updates = UpdatesSummary.NotSearched;
    private IReadOnlyList<UpdateItem> _pending = [];
    private HardwareSnapshot? _hardware;
    private SystemSnapshot? _system;
    private NetworkSnapshot? _network;
    private long? _internetMs;

    /// <summary>Initializes a new instance of the <see cref="OverviewViewModel"/> class.</summary>
    /// <param name="recommendations">Recommended action rules.</param>
    /// <param name="export">Full report.</param>
    /// <param name="feedback">Toasts.</param>
    /// <param name="navigator">Page navigation.</param>
    /// <param name="messenger">Shared messages.</param>
    /// <param name="time">Clock.</param>
    public OverviewViewModel(IRecommendationService recommendations, IExportService export, IFeedbackService feedback, IPageNavigator navigator, IMessenger messenger, TimeProvider time)
    {
        _recommendations = recommendations;
        _export = export;
        _feedback = feedback;
        _navigator = navigator;
        _messenger = messenger;
        _time = time;
        messenger.RegisterAll(this);
    }

    /// <inheritdoc />
    public PageKey Page => PageKey.Overview;

    /// <inheritdoc />
    public IAsyncRelayCommand? PrimaryCommand => null;

    /// <summary>Gets the recommended actions.</summary>
    public ObservableCollection<RecommendedAction> Recommendations { get; } = [];

    /// <summary>Gets the lettered volumes, by letter (F-ACC-19).</summary>
    public ObservableCollection<VolumeRow> Volumes { get; } = [];

    /// <summary>Gets the subtitle: device, edition, uptime (F-ACC-01).</summary>
    public string Subtitle => _system is { } s
        ? string.Join(" · ", new[] { s.DeviceName, s.Edition, s.BootTime is { } boot ? Formats.StartedAgo(_time.GetLocalNow() - boot) : null }.Where(p => p is not null))
        : Strings.Ovw_Loading;

    /// <summary>Gets the value of the updates tile: "-" before any search (F-ACC-08).</summary>
    public string UpdatesValue => _updates.HasSearched ? Formats.Whole(_updates.PendingCount) : Strings.Common_Dash;

    /// <summary>Gets the detail of the updates tile.</summary>
    public string UpdatesDetail => _updates is { HasSearched: true, KnownBytes: > 0 }
        ? Formats.Template(Strings.Upd_ToDownload, Formats.DownloadTotal(_updates.KnownBytes, _updates.HasUnknownSize))
        : string.Empty;

    /// <summary>Gets the value of the processor tile.</summary>
    public string CpuValue => _hardware?.Cpu.UsagePercent is { } u ? Formats.Number(u) : Strings.Common_Dash;

    /// <summary>Gets the detail of the processor tile.</summary>
    public string CpuDetail => _hardware?.Cpu is { Cores: { } cores, LogicalProcessors: { } threads }
        ? Formats.Template(Strings.Ovw_CpuDetail, cores, threads)
        : string.Empty;

    /// <summary>Gets the value of the memory tile.</summary>
    public string MemoryValue => _hardware is { } h ? Formats.Number(h.Memory.UsagePercent) : Strings.Common_Dash;

    /// <summary>Gets the detail of the memory tile.</summary>
    public string MemoryDetail => _hardware is { } h
        ? Formats.Template(Strings.Ovw_MemoryDetail, Formats.Gb(h.Memory.UsedBytes, 1), Formats.Gb(h.Memory.VisibleBytes, 1))
        : string.Empty;

    /// <summary>Gets the value of the network tile (F-ACC-09).</summary>
    public string NetworkValue => _network?.Status switch
    {
        null => Strings.Common_Dash,
        LinkStatus.Connected => Strings.Net_Connected,
        LinkStatus.Limited => Strings.Ovw_NetworkLimited,
        _ => Strings.Net_Disconnected,
    };

    /// <summary>Gets the detail of the network tile: adapter, link speed, Internet duration when tested.</summary>
    public string NetworkDetail
    {
        get
        {
            if (_network?.Primary is not { } primary)
            {
                return string.Empty;
            }

            var parts = new List<string> { primary.Name, Formats.LinkSpeed(primary.LinkSpeedBps) };
            if (_internetMs is { } ms)
            {
                parts.Add(Formats.Milliseconds(ms));
            }

            return string.Join(" · ", parts.Where(p => p.Length > 0));
        }
    }

    /// <inheritdoc />
    public Task OnNavigatedToAsync(CancellationToken ct)
    {
        Recompute();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Receive(UpdatesChangedMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        _updates = message.Summary;
        _pending = message.Pending;
        Recompute();
    }

    /// <inheritdoc />
    public void Receive(HardwareSnapshotMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        _hardware = message.Snapshot;
        Volumes.Clear();
        foreach (var volume in message.Snapshot.Volumes.Where(v => v.DriveLetter is not null).OrderBy(v => v.DriveLetter, StringComparer.OrdinalIgnoreCase))
        {
            Volumes.Add(VolumeRow.From(volume));
        }

        Recompute();
    }

    /// <inheritdoc />
    public void Receive(SystemSnapshotMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        _system = message.Snapshot;
        Recompute();
    }

    /// <inheritdoc />
    public void Receive(NetworkSnapshotMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        _network = message.Snapshot;
        _internetMs = message.InternetElapsedMs ?? _internetMs;
        Recompute();
    }

    // Opens the Updates page and starts the search (cahier E7).
    [RelayCommand]
    private void SearchUpdates()
    {
        _navigator.Navigate(PageKey.Updates);
        _messenger.Send(new SearchUpdatesRequestedMessage());
    }

    [RelayCommand]
    private async Task ExportReportAsync(CancellationToken ct)
    {
        var result = await _export.ExportReportAsync(ExportKind.Full, new ReportContent(_hardware, _system, _network, _pending), ct);
        PageFeedback.ShowExport(_feedback, _export, result, () => ExportReportCommand.ExecuteAsync(null));
    }

    [RelayCommand]
    private void OpenPage(PageKey page) => _navigator.Navigate(page);

    private void Recompute()
    {
        Recommendations.Clear();
        foreach (var action in _recommendations.Compute(_updates, _hardware, _system, _time.GetLocalNow()))
        {
            Recommendations.Add(action);
        }

        OnPropertyChanged(string.Empty);
    }
}
