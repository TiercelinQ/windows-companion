using CommunityToolkit.Mvvm.Messaging;
using NSubstitute;
using WindowsCompanion.Models;
using WindowsCompanion.Services;
using WindowsCompanion.ViewModels;

namespace WindowsCompanion.Tests.ViewModels;

public sealed class SettingsViewModelTests
{
    private readonly IPreferencesService _preferences = Substitute.For<IPreferencesService>();
    private readonly IAppThemeService _theme = Substitute.For<IAppThemeService>();
    private readonly IFeedbackService _feedback = Substitute.For<IFeedbackService>();
    private readonly IMessenger _messenger = new WeakReferenceMessenger();
    private readonly SettingsViewModel _viewModel;

    public SettingsViewModelTests()
    {
        _preferences.Current.Returns(new AppSettings { Exclusions = [new ExclusionEntry("Valve.Steam", "Steam", "Mise à jour automatique intégrée", DateTimeOffset.UnixEpoch)] });
        _viewModel = new SettingsViewModel(_preferences, _theme, _feedback, _messenger);
    }

    [Fact]
    public void Charge_les_reglages_sans_les_reecrire()
    {
        Assert.True(_viewModel.UseWinget);
        Assert.Equal("Valve.Steam · Mise à jour automatique intégrée", Assert.Single(_viewModel.Exclusions).Detail);
        _preferences.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public void Enregistre_un_reglage_des_qu_il_change()
    {
        var changed = false;
        _messenger.Register<SettingsChangedMessage>(this, (_, _) => changed = true);

        _viewModel.UseMsStore = false;

        _preferences.ReceivedWithAnyArgs(1).UpdateAsync(default!, default);
        Assert.True(changed);
    }

    [Fact]
    public void Garde_les_lignes_ignorees_quand_un_autre_reglage_change()
    {
        var row = _viewModel.Exclusions[0];

        _viewModel.SilentInstall = false;

        Assert.Same(row, Assert.Single(_viewModel.Exclusions));
    }

    [Fact]
    public void Applique_le_theme_choisi()
    {
        _viewModel.ThemeMode = ThemeMode.Dark;

        _theme.Received(1).SetModeAsync(ThemeMode.Dark, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reactive_une_application_ignoree()
    {
        await _viewModel.ReactivateCommand.ExecuteAsync(_viewModel.Exclusions[0]);

        await _preferences.Received(1).RemoveExclusionsAsync(Arg.Is<IEnumerable<string>>(ids => ids.Single() == "Valve.Steam"), Arg.Any<CancellationToken>());
        _feedback.Received(1).Show(Arg.Is<ResultError>(e => e.Message == "Steam réactivée"), Arg.Any<ToastAction[]>());
    }
}
