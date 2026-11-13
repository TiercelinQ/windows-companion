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
    private readonly IUpdateCheckService _updateCheck = Substitute.For<IUpdateCheckService>();
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
        var historyService = Substitute.For<IHistoryService>();
        historyService.ListAsync(Arg.Any<CancellationToken>()).Returns(new HistoryLoad([], WasReset: false));
        var history = new HistoryViewModel(historyService, export, feedback, _messenger, time);
        _shell = new ShellViewModel(
            updates, [updates, hardware, system, network, history], hardware, system, network, history, _navigator,
            _preferences, _elevation, _updateCheck, _lifetime, Substitute.For<IContentDialogService>(), _feedback, _messenger, NullLogger<ShellViewModel>.Instance);
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

    [Fact]
    public void Annonce_une_version_plus_recente_avec_une_action_telecharger()
    {
        _updateCheck.CheckAsync(true, Arg.Any<CancellationToken>()).Returns(Result<UpdateCheckResult>.Success(new(UpdateCheckStatus.Available, "9.9.9")));

        _messenger.Send(new AppUpdateCheckRequestedMessage());

        _feedback.Received(1).Show(
            Arg.Is<ResultError>(e => e.Type == FeedbackType.Info && e.Description!.Contains("9.9.9", StringComparison.Ordinal)),
            Arg.Is<ToastAction[]>(a => a.Length == 1 && a[0].IsPrimary));
    }

    [Fact]
    public async Task L_action_telecharger_ouvre_la_page_des_releases()
    {
        ToastAction[]? actions = null;
        _feedback.Show(Arg.Any<ResultError>(), Arg.Do<ToastAction[]>(a => actions = a));
        _updateCheck.CheckAsync(true, Arg.Any<CancellationToken>()).Returns(Result<UpdateCheckResult>.Success(new(UpdateCheckStatus.Available, "9.9.9")));
        _messenger.Send(new AppUpdateCheckRequestedMessage());

        await actions![0].Execute();

        _updateCheck.Received(1).OpenReleasesPage();
    }

    [Fact]
    public void La_demande_manuelle_confirme_que_l_application_est_a_jour()
    {
        _updateCheck.CheckAsync(true, Arg.Any<CancellationToken>()).Returns(Result<UpdateCheckResult>.Success(new(UpdateCheckStatus.UpToDate, AppConfig.AppVersion)));

        _messenger.Send(new AppUpdateCheckRequestedMessage());

        _feedback.Received(1).Show(Arg.Is<ResultError>(e => e.Type == FeedbackType.Success), Arg.Any<ToastAction[]>());
    }

    [Fact]
    public void La_demande_manuelle_signale_l_echec_de_la_verification()
    {
        var error = new ResultError(FeedbackType.Warning, "Vérification impossible", "GitHub n'a pas répondu.");
        _updateCheck.CheckAsync(true, Arg.Any<CancellationToken>()).Returns(Result<UpdateCheckResult>.Failure(error));

        _messenger.Send(new AppUpdateCheckRequestedMessage());

        _feedback.Received(1).Show(error, Arg.Any<ToastAction[]>());
    }

    [Fact]
    public async Task Reste_silencieux_au_demarrage_quand_la_verification_echoue()
    {
        _updateCheck.CheckAsync(false, Arg.Any<CancellationToken>()).Returns(Result<UpdateCheckResult>.Failure(new(FeedbackType.Warning, "Vérification impossible")));

        await _shell.InitializeAsync(PageKey.Overview, settingsWereReset: false, TestContext.Current.CancellationToken);

        _feedback.DidNotReceive().Show(Arg.Any<ResultError>(), Arg.Any<ToastAction[]>());
    }

    [Fact]
    public async Task Annonce_au_demarrage_une_version_plus_recente()
    {
        _updateCheck.CheckAsync(false, Arg.Any<CancellationToken>()).Returns(Result<UpdateCheckResult>.Success(new(UpdateCheckStatus.Available, "9.9.9")));

        await _shell.InitializeAsync(PageKey.Overview, settingsWereReset: false, TestContext.Current.CancellationToken);

        _feedback.Received(1).Show(Arg.Is<ResultError>(e => e.Type == FeedbackType.Info), Arg.Any<ToastAction[]>());
    }
}
