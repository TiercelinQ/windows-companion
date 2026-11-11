using CommunityToolkit.Mvvm.Messaging;
using NSubstitute;
using WindowsCompanion.Services;
using WindowsCompanion.Tests.Support;
using WindowsCompanion.ViewModels;

namespace WindowsCompanion.Tests.ViewModels;

public sealed class SystemViewModelTests
{
    private readonly ISystemInfoService _system = Substitute.For<ISystemInfoService>();
    private readonly ManualTimeProvider _time = new();
    private readonly SystemViewModel _viewModel;

    public SystemViewModelTests()
    {
        _system.GetSnapshotAsync(default).ReturnsForAnyArgs(_ => Samples.System() with { TakenAt = _time.GetLocalNow() });
        _viewModel = new SystemViewModel(_system, Substitute.For<IExportService>(), Substitute.For<IClipboardService>(), Substitute.For<IFeedbackService>(), new WeakReferenceMessenger(), _time);
    }

    [Fact]
    public async Task Filtre_sur_le_libelle_et_la_valeur_sans_tenir_compte_de_la_casse()
    {
        await _viewModel.InitializeAsync(TestContext.Current.CancellationToken);

        _viewModel.FilterText = "PARE-FEU";

        var card = Assert.Single(_viewModel.Cards);
        Assert.Equal("Sécurité", card.Title);
        Assert.Single(card.Rows);
    }

    [Fact]
    public async Task Affiche_l_etat_vide_quand_rien_ne_correspond()
    {
        await _viewModel.InitializeAsync(TestContext.Current.CancellationToken);

        _viewModel.FilterText = "zzz";

        Assert.Empty(_viewModel.Cards);
        Assert.True(_viewModel.HasNoMatch);
    }

    [Fact]
    public async Task Relit_seulement_apres_cinq_minutes()
    {
        await _viewModel.InitializeAsync(TestContext.Current.CancellationToken);

        await _viewModel.OnNavigatedToAsync(TestContext.Current.CancellationToken);
        _time.Advance(TimeSpan.FromMinutes(AppConfig.SystemStaleMinutes + 1));
        await _viewModel.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        await _system.Received(2).GetSnapshotAsync(Arg.Any<CancellationToken>());
    }
}
