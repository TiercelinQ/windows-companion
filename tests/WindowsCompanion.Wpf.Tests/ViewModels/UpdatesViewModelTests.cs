using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WindowsCompanion.Models;
using WindowsCompanion.Services;
using WindowsCompanion.Tests.Support;
using WindowsCompanion.ViewModels;

namespace WindowsCompanion.Tests.ViewModels;

public sealed class UpdatesViewModelTests
{
    private readonly IWingetService _winget = Substitute.For<IWingetService>();
    private readonly IPreferencesService _preferences = Substitute.For<IPreferencesService>();
    private readonly IHistoryService _history = Substitute.For<IHistoryService>();
    private readonly IRunningAppService _running = Substitute.For<IRunningAppService>();
    private readonly IFeedbackService _feedback = Substitute.For<IFeedbackService>();
    private readonly IMessenger _messenger = new WeakReferenceMessenger();
    private readonly UpdatesViewModel _viewModel;

    public UpdatesViewModelTests()
    {
        _preferences.Current.Returns(new AppSettings());
        _winget.SearchUpgradesAsync(default!, default, default!, default)
            .ReturnsForAnyArgs(Result<UpgradeSearchResult>.Success(new UpgradeSearchResult([Samples.Firefox, Samples.Vscode, Samples.Spotify], [])));
        _winget.InstallAsync(default!, default, default!, default).ReturnsForAnyArgs(new InstallOutcome(InstallResultKind.Installed));
        _viewModel = new UpdatesViewModel(
            _winget, _preferences, _history, _running, _feedback, Substitute.For<IClipboardService>(),
            Substitute.For<IPageNavigator>(), _messenger, new ManualTimeProvider(), NullLogger<UpdatesViewModel>.Instance);
    }

    private UpdateItemViewModel Item(string id) => _viewModel.VisibleItems.Single(i => i.Id == id);

    [Fact]
    public async Task Affiche_les_resultats_de_la_recherche_sans_les_applications_ignorees()
    {
        _preferences.Current.Returns(new AppSettings { Exclusions = [new ExclusionEntry("9NCBCSZSJRSB", "Spotify", "r", DateTimeOffset.UnixEpoch)] });

        await _viewModel.SearchCommand.ExecuteAsync(null);

        Assert.Equal(["Mozilla.Firefox", "Microsoft.VisualStudioCode"], _viewModel.VisibleItems.Select(i => i.Id));
        Assert.Equal(2, _viewModel.PendingCount);
        _feedback.Received(1).Show(Arg.Is<ResultError>(e => e.Type == FeedbackType.Info), Arg.Any<ToastAction[]>());
    }

    [Fact]
    public async Task Affiche_une_erreur_quand_la_recherche_echoue()
    {
        _winget.SearchUpgradesAsync(default!, default, default!, default)
            .ReturnsForAnyArgs(Result<UpgradeSearchResult>.Failure(new(FeedbackType.Danger, "Recherche impossible")));

        await _viewModel.SearchCommand.ExecuteAsync(null);

        Assert.Empty(_viewModel.VisibleItems);
        _feedback.Received(1).Show(Arg.Is<ResultError>(e => e.Type == FeedbackType.Danger), Arg.Any<ToastAction[]>());
    }

    [Fact]
    public async Task Desactive_la_recherche_sans_source_active()
    {
        _preferences.Current.Returns(new AppSettings { UseWinget = false, UseMsStore = false });
        _messenger.Send(new SettingsChangedMessage());

        Assert.False(_viewModel.SearchCommand.CanExecute(null));
        Assert.NotNull(_viewModel.SearchToolTip);
        await _winget.DidNotReceiveWithAnyArgs().SearchUpgradesAsync(default!, default, default!, default);
    }

    [Fact]
    public async Task Installe_un_element_et_l_ajoute_a_l_historique()
    {
        await _viewModel.SearchCommand.ExecuteAsync(null);

        _viewModel.InstallItemCommand.Execute(Item("Mozilla.Firefox"));
        await _viewModel.QueueTask;

        Assert.Equal(UpdateState.Installed, Item("Mozilla.Firefox").State);
        await _history.Received(1).AddAsync(Arg.Is<HistoryEntry>(h => h.AppId == "Mozilla.Firefox" && h.Result == HistoryResult.Success), Arg.Any<CancellationToken>());
        _feedback.Received(1).Show(Arg.Is<ResultError>(e => e.Message == "Mises à jour installées"), Arg.Any<ToastAction[]>());
        Assert.False(_viewModel.IsInstalling);
    }

    [Fact]
    public async Task Reste_en_installation_jusqu_a_la_fin_du_traitement()
    {
        var gate = new TaskCompletionSource<InstallOutcome>();
        _winget.InstallAsync(default!, default, default!, default).ReturnsForAnyArgs(gate.Task);
        await _viewModel.SearchCommand.ExecuteAsync(null);

        _viewModel.InstallItemCommand.Execute(Item("Mozilla.Firefox"));

        Assert.Equal(UpdateState.Installing, Item("Mozilla.Firefox").State);
        Assert.Equal("En cours", Item("Mozilla.Firefox").StatusText);
        Assert.True(Item("Mozilla.Firefox").CanCancel);
        Assert.Equal(0, _viewModel.GlobalPercent);

        gate.SetResult(new InstallOutcome(InstallResultKind.Installed));
        await _viewModel.QueueTask;

        Assert.Equal(UpdateState.Installed, Item("Mozilla.Firefox").State);
        Assert.False(Item("Mozilla.Firefox").CanCancel);
    }

    [Fact]
    public async Task Signale_l_interruption_d_une_installation_annulee_seule()
    {
        _winget.InstallAsync(default!, default, default!, default).ReturnsForAnyArgs(async call =>
        {
            await Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>()).ContinueWith(_ => { }, TaskScheduler.Default);
            return new InstallOutcome(InstallResultKind.Cancelled);
        });
        await _viewModel.SearchCommand.ExecuteAsync(null);
        _viewModel.InstallItemCommand.Execute(Item("Mozilla.Firefox"));

        _viewModel.CancelItemCommand.Execute(Item("Mozilla.Firefox"));
        await _viewModel.QueueTask;

        Assert.Equal(UpdateState.Cancelled, Item("Mozilla.Firefox").State);
        _feedback.Received(1).Show(Arg.Is<ResultError>(e => e.Message == "Installation interrompue"), Arg.Any<ToastAction[]>());
    }

    [Fact]
    public async Task Signale_l_echec_et_le_lot_termine_avec_des_erreurs()
    {
        _winget.InstallAsync(Samples.Vscode, default, default!, default)
            .ReturnsForAnyArgs(call => call.Arg<UpdateItem>().Id == Samples.Vscode.Id
                ? new InstallOutcome(InstallResultKind.Failed, "1603")
                : new InstallOutcome(InstallResultKind.Installed));
        await _viewModel.SearchCommand.ExecuteAsync(null);

        _viewModel.InstallAllCommand.Execute(null);
        await _viewModel.QueueTask;

        Assert.Equal(UpdateState.Failed, Item("Microsoft.VisualStudioCode").State);
        Assert.Equal("Échec 1603", Item("Microsoft.VisualStudioCode").StatusText);
        _feedback.Received(1).Show(Arg.Is<ResultError>(e => e.Type == FeedbackType.Danger && e.Message == "Échec : Visual Studio Code"), Arg.Any<ToastAction[]>());
        _feedback.Received(1).Show(Arg.Is<ResultError>(e => e.Message == "Terminé avec des erreurs"), Arg.Any<ToastAction[]>());
    }

    [Fact]
    public async Task Passe_en_application_ouverte_quand_elle_tourne_encore()
    {
        _running.IsRunningAsync(default!, default).ReturnsForAnyArgs(true);
        await _viewModel.SearchCommand.ExecuteAsync(null);

        _viewModel.InstallItemCommand.Execute(Item("Mozilla.Firefox"));
        await _viewModel.QueueTask;

        Assert.Equal(UpdateState.AppOpen, Item("Mozilla.Firefox").State);
        Assert.Equal(2, _viewModel.PendingCount);
    }

    [Fact]
    public async Task Retire_de_la_file_un_element_en_attente()
    {
        var gate = new TaskCompletionSource<InstallOutcome>();
        _winget.InstallAsync(default!, default, default!, default).ReturnsForAnyArgs(gate.Task);
        await _viewModel.SearchCommand.ExecuteAsync(null);
        _viewModel.InstallAllCommand.Execute(null);

        _viewModel.CancelItemCommand.Execute(Item("9NCBCSZSJRSB"));
        gate.SetResult(new InstallOutcome(InstallResultKind.Installed));
        await _viewModel.QueueTask;

        Assert.Equal(UpdateState.Cancelled, Item("9NCBCSZSJRSB").State);
        _feedback.Received(1).Show(Arg.Is<ResultError>(e => e.Message == "Retiré de la file"), Arg.Any<ToastAction[]>());
        await _history.DidNotReceive().AddAsync(Arg.Is<HistoryEntry>(h => h.AppId == "9NCBCSZSJRSB"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Annule_tout_avec_un_seul_toast()
    {
        _winget.InstallAsync(default!, default, default!, default).ReturnsForAnyArgs(async call =>
        {
            await Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>()).ContinueWith(_ => { }, TaskScheduler.Default);
            return new InstallOutcome(InstallResultKind.Cancelled);
        });
        await _viewModel.SearchCommand.ExecuteAsync(null);
        _viewModel.InstallAllCommand.Execute(null);

        _viewModel.CancelAllCommand.Execute(null);
        await _viewModel.QueueTask;

        Assert.All(_viewModel.VisibleItems, i => Assert.Equal(UpdateState.Cancelled, i.State));
        _feedback.Received(1).Show(Arg.Is<ResultError>(e => e.Message == "Installations annulées"), Arg.Any<ToastAction[]>());
        _feedback.DidNotReceive().Show(Arg.Is<ResultError>(e => e.Message == "Installation interrompue"), Arg.Any<ToastAction[]>());
        await _history.Received(1).AddAsync(Arg.Is<HistoryEntry>(h => h.Result == HistoryResult.Cancelled), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Ne_traite_que_les_lignes_cochees_visibles()
    {
        await _viewModel.SearchCommand.ExecuteAsync(null);
        Item("Mozilla.Firefox").IsSelected = true;
        Item("9NCBCSZSJRSB").IsSelected = true;

        _viewModel.SourceFilter = SourceFilter.Winget;

        Assert.Equal("Installer la sélection (1)", _viewModel.InstallSelectionLabel);
    }

    [Fact]
    public async Task Ignore_la_selection_et_l_annule_depuis_le_toast()
    {
        ToastAction? undo = null;
        _feedback.When(f => f.Show(Arg.Is<ResultError>(e => e.Message == "1 application ignorée" && e.Description == "Elle ne sera plus proposée. Gérez la liste dans Paramètres."), Arg.Any<ToastAction[]>()))
            .Do(call => undo = call.Arg<ToastAction[]>()[0]);
        await _viewModel.SearchCommand.ExecuteAsync(null);
        Item("Mozilla.Firefox").IsSelected = true;

        await _viewModel.IgnoreSelectionCommand.ExecuteAsync(null);
        Assert.DoesNotContain(_viewModel.VisibleItems, i => i.Id == "Mozilla.Firefox");
        await _preferences.Received(1).AddExclusionsAsync(Arg.Any<IEnumerable<ExclusionEntry>>(), Arg.Any<CancellationToken>());

        await undo!.Execute();

        Assert.Contains(_viewModel.VisibleItems, i => i.Id == "Mozilla.Firefox");
        await _preferences.Received(1).RemoveExclusionsAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
    }
}
