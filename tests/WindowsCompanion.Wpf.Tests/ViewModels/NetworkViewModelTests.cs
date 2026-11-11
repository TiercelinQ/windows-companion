using CommunityToolkit.Mvvm.Messaging;
using NSubstitute;
using WindowsCompanion.Models;
using WindowsCompanion.Services;
using WindowsCompanion.Tests.Support;
using WindowsCompanion.ViewModels;

namespace WindowsCompanion.Tests.ViewModels;

public sealed class NetworkViewModelTests
{
    private readonly INetworkService _network = Substitute.For<INetworkService>();
    private readonly IElevationService _elevation = Substitute.For<IElevationService>();
    private readonly IFeedbackService _feedback = Substitute.For<IFeedbackService>();
    private readonly IMessenger _messenger = new WeakReferenceMessenger();
    private readonly NetworkViewModel _viewModel;

    public NetworkViewModelTests()
    {
        _network.GetAdaptersAsync(default).ReturnsForAnyArgs(new NetworkSnapshot(DateTimeOffset.UnixEpoch, [Samples.Ethernet()]));
        _viewModel = new NetworkViewModel(_network, _elevation, Substitute.For<IExportService>(), _feedback, _messenger);
    }

    private void TestsReturn(params TestStatus[] statuses) =>
        _network.RunConnectivityTestsAsync(default!, default!, default).ReturnsForAnyArgs(
            [.. Enum.GetValues<TestKind>().Zip(statuses, (kind, status) => new ConnectivityTest(kind, status, "détail", 23))]);

    [Fact]
    public void Affiche_non_teste_avant_le_premier_test()
    {
        Assert.All(_viewModel.Tests, t => Assert.Equal(TestStatus.NotTested, t.Status));
        Assert.Equal("Non testé", _viewModel.StateText);
    }

    [Fact]
    public async Task Annonce_la_connectivite_verifiee()
    {
        TestsReturn(TestStatus.Succeeded, TestStatus.Succeeded, TestStatus.Succeeded, TestStatus.Succeeded);

        await _viewModel.TestConnectivityCommand.ExecuteAsync(null);

        Assert.Equal(ConnectivityState.Connected, _viewModel.State);
        _feedback.Received(1).Show(Arg.Is<ResultError>(e => e.Type == FeedbackType.Success && e.Message == "Connectivité vérifiée"), Arg.Any<ToastAction[]>());
    }

    [Fact]
    public async Task Avertit_quand_internet_est_inaccessible()
    {
        TestsReturn(TestStatus.Succeeded, TestStatus.Succeeded, TestStatus.Succeeded, TestStatus.Failed);

        await _viewModel.TestConnectivityCommand.ExecuteAsync(null);

        _feedback.Received(1).Show(Arg.Is<ResultError>(e => e.Type == FeedbackType.Warning && e.Message == "Internet inaccessible"), Arg.Any<ToastAction[]>());
    }

    [Fact]
    public async Task Propose_la_relance_en_administrateur_sans_droits()
    {
        _elevation.IsElevated.Returns(false);
        var disabled = new AdapterBlock(Samples.Ethernet(up: false) with { IsDisabled = true }, "Désactivé", []);

        await _viewModel.EnableAdapterCommand.ExecuteAsync(disabled);

        await _network.DidNotReceiveWithAnyArgs().EnableAdapterAsync(default!, default);
        _feedback.Received(1).Show(Arg.Is<ResultError>(e => e.Message == "Droits administrateur requis"), Arg.Is<ToastAction[]>(a => a.Length == 2));
    }
}
