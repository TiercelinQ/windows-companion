using CommunityToolkit.Mvvm.Messaging;
using NSubstitute;
using WindowsCompanion.Models;
using WindowsCompanion.Services;
using WindowsCompanion.Tests.Support;
using WindowsCompanion.ViewModels;

namespace WindowsCompanion.Tests.ViewModels;

public sealed class OverviewViewModelTests
{
    private readonly IMessenger _messenger = new WeakReferenceMessenger();
    private readonly IPageNavigator _navigator = Substitute.For<IPageNavigator>();
    private readonly OverviewViewModel _viewModel;

    public OverviewViewModelTests() => _viewModel = new OverviewViewModel(
        new RecommendationService(), Substitute.For<IExportService>(), Substitute.For<IFeedbackService>(), _navigator, _messenger, new ManualTimeProvider());

    [Fact]
    public void Affiche_un_tiret_avant_toute_recherche()
    {
        Assert.Equal("-", _viewModel.UpdatesValue);
        Assert.Equal("-", _viewModel.NetworkValue);
    }

    [Fact]
    public void Remplit_les_tuiles_depuis_les_instantanes()
    {
        _messenger.Send(new UpdatesChangedMessage(new UpdatesSummary(true, 10, 1471 * Samples.Mib, false, "Docker Desktop", 590 * Samples.Mib), []));
        _messenger.Send(new HardwareSnapshotMessage(Samples.Hardware(Samples.Volume("D:", 1863, 1190, disk: 1), Samples.Volume("C:", 931, 612))));
        _messenger.Send(new NetworkSnapshotMessage(new NetworkSnapshot(DateTimeOffset.UnixEpoch, [Samples.Ethernet()]), 23));

        Assert.Equal("10", _viewModel.UpdatesValue);
        Assert.Equal("1,4 Go à télécharger", _viewModel.UpdatesDetail);
        Assert.Equal("23", _viewModel.CpuValue);
        Assert.Equal("14 cœurs · 20 threads", _viewModel.CpuDetail);
        Assert.Equal("Connecté", _viewModel.NetworkValue);
        Assert.Equal("Ethernet · 1 Gb/s · 23 ms", _viewModel.NetworkDetail);
        Assert.Equal(["C:", "D:"], _viewModel.Volumes.Select(v => v.Info.DriveLetter));
    }

    [Fact]
    public void Indique_limite_sans_passerelle()
    {
        _messenger.Send(new NetworkSnapshotMessage(new NetworkSnapshot(DateTimeOffset.UnixEpoch, [Samples.Ethernet(gateway: null)]), null));

        Assert.Equal("Limité", _viewModel.NetworkValue);
    }

    [Fact]
    public void Ouvre_les_mises_a_jour_et_lance_la_recherche()
    {
        var requested = false;
        _messenger.Register<SearchUpdatesRequestedMessage>(this, (_, _) => requested = true);

        _viewModel.SearchUpdatesCommand.Execute(null);

        _navigator.Received(1).Navigate(PageKey.Updates);
        Assert.True(requested);
    }
}
