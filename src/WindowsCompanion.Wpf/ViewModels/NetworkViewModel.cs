using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WindowsCompanion.Helpers;
using WindowsCompanion.Models;
using WindowsCompanion.Resources;
using WindowsCompanion.Services;

namespace WindowsCompanion.ViewModels;

/// <summary>An adapter block.</summary>
/// <param name="Info">The adapter.</param>
/// <param name="StatusText">Chip text: connected, disconnected or disabled.</param>
/// <param name="Rows">Lines (13 when connected, 4 otherwise).</param>
public sealed record AdapterBlock(AdapterInfo Info, string StatusText, IReadOnlyList<InfoRow> Rows)
{
    /// <summary>Gets the accessible name of the enable button.</summary>
    public string EnableName => Formats.Template(Strings.Net_EnableOperation, Info.Name);
}

/// <summary>A line of the Connectivity card.</summary>
/// <param name="Kind">Which test.</param>
/// <param name="Name">Test name.</param>
/// <param name="Status">State.</param>
/// <param name="Detail">Detail text.</param>
/// <param name="ElapsedText">Duration text, empty when not shown.</param>
public sealed record TestRow(TestKind Kind, string Name, TestStatus Status, string Detail, string ElapsedText);

/// <summary>Network page: connectivity tests, adapters, enable, DNS cache, export (cahier 4.6).</summary>
public sealed partial class NetworkViewModel : ObservableObject, IPageViewModel
{
    private readonly INetworkService _network;
    private readonly IElevationService _elevation;
    private readonly IExportService _export;
    private readonly IFeedbackService _feedback;
    private readonly IMessenger _messenger;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Adapters))]
    private NetworkSnapshot? _snapshot;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateText))]
    private ConnectivityState _state = ConnectivityState.NotTested;

    /// <summary>Initializes a new instance of the <see cref="NetworkViewModel"/> class.</summary>
    /// <param name="network">Network reading and actions.</param>
    /// <param name="elevation">Elevation state.</param>
    /// <param name="export">Reports.</param>
    /// <param name="feedback">Toasts.</param>
    /// <param name="messenger">Shared messages.</param>
    public NetworkViewModel(INetworkService network, IElevationService elevation, IExportService export, IFeedbackService feedback, IMessenger messenger)
    {
        _network = network;
        _elevation = elevation;
        _export = export;
        _feedback = feedback;
        _messenger = messenger;
        foreach (var kind in Enum.GetValues<TestKind>())
        {
            Tests.Add(Row(new ConnectivityTest(kind, TestStatus.NotTested)));
        }
    }

    /// <inheritdoc />
    public PageKey Page => PageKey.Network;

    /// <inheritdoc />
    public IAsyncRelayCommand PrimaryCommand => TestConnectivityCommand;

    /// <summary>Gets the four test lines (F-RES-04: "Non testé" before the first test).</summary>
    public ObservableCollection<TestRow> Tests { get; } = [];

    /// <summary>Gets the adapter blocks, connected first.</summary>
    public IReadOnlyList<AdapterBlock> Adapters => Snapshot is { } s
        ? [.. s.Adapters.Select(a => new AdapterBlock(
            a,
            a.IsDisabled ? Strings.Net_Disabled : a.IsUp ? Strings.Net_Connected : Strings.Net_Disconnected,
            InfoRows.Adapter(a, maskMac: false)))]
        : [];

    /// <summary>Gets the chip text of the Connectivity card (F-RES-03).</summary>
    public string StateText => State switch
    {
        ConnectivityState.Connected => Strings.Net_StateConnected,
        ConnectivityState.InternetUnreachable => Strings.Net_StateInternet,
        ConnectivityState.LocalUnreachable => Strings.Net_StateLocal,
        ConnectivityState.NoNetwork => Strings.Net_StateNone,
        _ => Strings.Net_StateNotTested,
    };

    /// <summary>Reads the adapters at startup, for the overview tile.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task completed once the snapshot is published.</returns>
    public Task InitializeAsync(CancellationToken ct) => LoadCommand.ExecuteAsync(null);

    /// <inheritdoc />
    public Task OnNavigatedToAsync(CancellationToken ct) =>
        LoadCommand.CanExecute(null) && !TestConnectivityCommand.IsRunning ? LoadCommand.ExecuteAsync(null) : Task.CompletedTask;

    private static string TestName(TestKind kind) => kind switch
    {
        TestKind.Adapter => Strings.Net_TestAdapter,
        TestKind.Gateway => Strings.Net_TestGateway,
        TestKind.Dns => Strings.Net_TestDns,
        _ => Strings.Net_TestInternet,
    };

    private static TestRow Row(ConnectivityTest test) => new(
        test.Kind,
        TestName(test.Kind),
        test.Status,
        test.Status switch
        {
            TestStatus.NotTested => Strings.Net_NotTested,
            TestStatus.Waiting => Strings.State_Queued,
            TestStatus.Running => Strings.Net_Running,
            _ => test.Detail ?? string.Empty,
        },
        test.ElapsedMs is { } ms && test.Status == TestStatus.Succeeded ? Formats.Milliseconds(ms) : string.Empty);

    [RelayCommand]
    private async Task LoadAsync(CancellationToken ct)
    {
        try
        {
            Snapshot = await _network.GetAdaptersAsync(ct);
            _messenger.Send(new NetworkSnapshotMessage(Snapshot, null));
        }
        catch (OperationCanceledException)
        {
            // Not an error.
        }
    }

    [RelayCommand]
    private async Task TestConnectivityAsync(CancellationToken ct)
    {
        try
        {
            for (var i = 0; i < Tests.Count; i++)
            {
                Tests[i] = Row(new ConnectivityTest(Tests[i].Kind, TestStatus.Waiting));
            }

            var snapshot = await _network.GetAdaptersAsync(ct);
            Snapshot = snapshot;
            var progress = new Progress<ConnectivityTest>(Replace);
            var results = await _network.RunConnectivityTestsAsync(snapshot, progress, ct);
            foreach (var result in results)
            {
                Replace(result);
            }

            State = Connectivity.Evaluate(results);
            var internetMs = results.FirstOrDefault(r => r is { Kind: TestKind.Internet, Status: TestStatus.Succeeded })?.ElapsedMs;
            _messenger.Send(new NetworkSnapshotMessage(snapshot, internetMs));

            switch (State)
            {
                case ConnectivityState.Connected:
                    _feedback.Show(new(FeedbackType.Success, Strings.Toast_Connected_Title, Formats.Template(Strings.Toast_Connected_Message, Formats.Milliseconds(internetMs ?? 0))));
                    break;
                case ConnectivityState.InternetUnreachable:
                    _feedback.Show(new(FeedbackType.Warning, Strings.Toast_InternetDown_Title, Strings.Toast_InternetDown_Message));
                    break;
                case ConnectivityState.LocalUnreachable:
                    _feedback.Show(new(FeedbackType.Danger, Strings.Toast_LocalDown_Title, Formats.Template(Strings.Toast_LocalDown_Message, snapshot.Primary?.Gateway ?? Strings.Common_Dash)));
                    break;
                default:
                    _feedback.Show(new(FeedbackType.Danger, Strings.Toast_NoNetwork_Title, Strings.Toast_NoNetwork_Message));
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // Not an error.
        }
    }

    [RelayCommand]
    private async Task EnableAdapterAsync(AdapterBlock? block, CancellationToken ct)
    {
        if (block is null)
        {
            return;
        }

        if (!_elevation.IsElevated)
        {
            ShowAdminRequired(block.EnableName);
            return;
        }

        var result = await _network.EnableAdapterAsync(block.Info, ct);
        if (result.Ok)
        {
            _feedback.Show(new(FeedbackType.Success, Strings.Toast_AdapterEnabled_Title, Formats.Template(Strings.Toast_AdapterEnabled_Message, block.Info.Name)));
            await LoadCommand.ExecuteAsync(null);
        }
        else
        {
            _feedback.Show(result.Error!);
        }
    }

    [RelayCommand]
    private async Task FlushDnsAsync(CancellationToken ct)
    {
        var result = await _network.FlushDnsAsync(ct);
        if (result.Ok)
        {
            _feedback.Show(new(FeedbackType.Success, Strings.Toast_DnsFlushed_Title, Strings.Toast_DnsFlushed_Message));
        }
        else if (!_elevation.IsElevated)
        {
            ShowAdminRequired(Strings.Net_FlushOperation);
        }
        else
        {
            _feedback.Show(result.Error!);
        }
    }

    [RelayCommand]
    private async Task ExportAsync(CancellationToken ct)
    {
        var result = await _export.ExportReportAsync(ExportKind.Network, new ReportContent(null, null, Snapshot, null), ct);
        PageFeedback.ShowExport(_feedback, _export, result, () => ExportCommand.ExecuteAsync(null));
    }

    // N-15: the action is offered with a relaunch as administrator, handled by the shell.
    private void ShowAdminRequired(string operation) => _feedback.Show(
        new(FeedbackType.Warning, Strings.Toast_AdminRequired_Title, Formats.Template(Strings.Toast_AdminRequired_Message, operation)),
        new ToastAction(Strings.Status_Relaunch, true, () =>
        {
            _messenger.Send(new ElevationRequestedMessage(PageKey.Network));
            return Task.CompletedTask;
        }),
        new ToastAction(Strings.Common_Cancel, false, () => Task.CompletedTask));

    private void Replace(ConnectivityTest test)
    {
        var index = Tests.ToList().FindIndex(t => t.Kind == test.Kind);
        if (index >= 0)
        {
            Tests[index] = Row(test);
        }
    }
}
