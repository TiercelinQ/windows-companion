using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WindowsCompanion.Models;
using WindowsCompanion.Services;
using WindowsCompanion.Tests.Support;
using WindowsCompanion.ViewModels;
using Wpf.Ui;

namespace WindowsCompanion.Tests.ViewModels;

public sealed class ShellViewModelTests
{
    private readonly IMessenger _messenger = new WeakReferenceMessenger();
    private readonly IPageNavigator _navigator = Substitute.For<IPageNavigator>();
    private readonly IPreferencesService _preferences = Substitute.For<IPreferencesService>();
    private readonly IElevationService _elevation = Substitute.For<IElevationService>();
    private readonly IFeedbackService _feedback = Substitute.For<IFeedbackService>();
    private readonly IAppLifetimeService _lifetime = Substitute.For<IAppLifetimeService>();
    private readonly ShellViewModel _shell;

    public ShellViewModelTests()
    {
        _preferences.Current.Returns(new AppSettings());
        var time = new ManualTimeProvider();
        var feedback = Substitute.For<IFeedbackService>();
        var export = Substitute.For<IExportService>();
        var updates = new UpdatesViewModel(Substitute.For<IWingetService>(), _preferences, Substitute.For<IHistoryService>(), Substitute.For<IRunningAppService>(), feedback, Substitute.For<IClipboardService>(), _navigator, _messenger, time, NullLogger<UpdatesViewModel>.Instance);
        var hardware = new HardwareViewModel(Substitute.For<IHardwareService>(), export, Substitute.For<IClipboardService>(), feedback, _messenger, time);
        var system = new SystemViewModel(Substitute.For<ISystemInfoService>(), export, Substitute.For<IClipboardService>(), feedback, _messenger, time);
        var network = new NetworkViewModel(Substitute.For<INetworkService>(), _elevation, export, feedback, _messenger);
        var history = new HistoryViewModel(Substitute.For<IHistoryService>(), export, feedback, _messenger, time);
        _shell = new ShellViewModel(
            updates, [updates, hardware, system, network, history], hardware, system, network, history, _navigator,
            _preferences, _elevation, _lifetime, Substitute.For<IContentDialogService>(), _feedback, _messenger, NullLogger<ShellViewModel>.Instance);
    }

    [Fact]
    public void Replie_le_volet_et_passe_sur_une_colonne_en_fenetre_etroite()
    {
        _shell.OnWindowWidthChanged(1280);
        Assert.True(_shell.IsPaneOpen);
        Assert.Equal(4, _shell.TileColumns);

        _shell.OnWindowWidthChanged(880);

        Assert.False(_shell.IsPaneOpen);
        Assert.Equal(1, _shell.ContentColumns);
        Assert.Equal(2, _shell.TileColumns);
    }

    [Fact]
    public void Affiche_les_droits_standard()
    {
        _elevation.IsElevated.Returns(false);

        Assert.Equal("Droits standard", _shell.RightsText);
    }

    [Fact]
    public async Task Reste_ouverte_quand_l_elevation_est_refusee()
    {
        _elevation.RelaunchElevated(PageKey.Network).Returns(Result<Unit>.Failure(new(FeedbackType.Warning, "Élévation refusée", "L'application reste en droits standard.")));

        await _shell.RelaunchAsAdminCommand.ExecuteAsync(PageKey.Network);

        _feedback.Received(1).Show(Arg.Is<ResultError>(e => e.Message == "Élévation refusée"), Arg.Any<ToastAction[]>());
        _lifetime.DidNotReceive().CloseMainWindow();
    }

    [Fact]
    public async Task Ferme_l_application_apres_la_relance_en_administrateur()
    {
        _elevation.RelaunchElevated(PageKey.Overview).Returns(Result<Unit>.Success(Unit.Value));

        await _shell.RelaunchAsAdminCommand.ExecuteAsync(PageKey.Overview);

        Assert.True(_shell.ExitConfirmed);
        _lifetime.Received(1).CloseMainWindow();
    }
}
