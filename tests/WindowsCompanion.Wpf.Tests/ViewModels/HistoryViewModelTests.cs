using CommunityToolkit.Mvvm.Messaging;
using NSubstitute;
using WindowsCompanion.Models;
using WindowsCompanion.Services;
using WindowsCompanion.Tests.Support;
using WindowsCompanion.ViewModels;

namespace WindowsCompanion.Tests.ViewModels;

public sealed class HistoryViewModelTests
{
    private readonly IHistoryService _history = Substitute.For<IHistoryService>();
    private readonly IFeedbackService _feedback = Substitute.For<IFeedbackService>();
    private readonly ManualTimeProvider _time = new();
    private readonly HistoryViewModel _viewModel;

    public HistoryViewModelTests()
    {
        _history.ListAsync(default).ReturnsForAnyArgs(new HistoryLoad(
            [new HistoryEntry(_time.GetUtcNow(), "Git.Git", "Git", "2.49.0", "2.50.1", UpdateSource.Winget, HistoryResult.Success, null, 28)],
            false));
        _viewModel = new HistoryViewModel(_history, Substitute.For<IExportService>(), _feedback, new WeakReferenceMessenger(), _time);
    }

    [Fact]
    public async Task Affiche_les_entrees_formatees()
    {
        await _viewModel.InitializeAsync(TestContext.Current.CancellationToken);

        var row = Assert.Single(_viewModel.Entries);
        Assert.Equal("2.49.0 vers 2.50.1", row.VersionText);
        Assert.Equal("Réussie", row.ResultText);
        Assert.Equal("28 s", row.DurationText);
        Assert.False(_viewModel.IsEmpty);
    }

    [Fact]
    public async Task Efface_au_second_appui_seulement()
    {
        await _viewModel.InitializeAsync(TestContext.Current.CancellationToken);

        var first = _viewModel.ClearCommand.ExecuteAsync(null);
        Assert.True(_viewModel.IsClearArmed);
        Assert.Equal("Confirmer l'effacement", _viewModel.ClearLabel);

        await _viewModel.ClearCommand.ExecuteAsync(null);
        _time.Advance(TimeSpan.FromMilliseconds(AppConfig.ClearConfirmMs));
        await first;

        await _history.Received(1).ClearAsync(Arg.Any<CancellationToken>());
        Assert.True(_viewModel.IsEmpty);
        _feedback.Received(1).Show(Arg.Is<ResultError>(e => e.Message == "Historique effacé"), Arg.Any<ToastAction[]>());
    }

    [Fact]
    public async Task Desarme_apres_trois_secondes_sans_second_appui()
    {
        var first = _viewModel.ClearCommand.ExecuteAsync(null);

        _time.Advance(TimeSpan.FromMilliseconds(AppConfig.ClearConfirmMs));
        await first;

        Assert.False(_viewModel.IsClearArmed);
        await _history.DidNotReceive().ClearAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Signale_un_historique_reinitialise()
    {
        _history.ListAsync(default).ReturnsForAnyArgs(new HistoryLoad([], true));

        await _viewModel.InitializeAsync(TestContext.Current.CancellationToken);

        _feedback.Received(1).Show(Arg.Is<ResultError>(e => e.Message == "Réglages réinitialisés"), Arg.Any<ToastAction[]>());
    }
}
