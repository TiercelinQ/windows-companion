using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WindowsCompanion.Models;
using WindowsCompanion.Services;
using WindowsCompanion.Services.Data;
using WindowsCompanion.Tests.Support;

namespace WindowsCompanion.Tests.Services;

public sealed class PreferencesServiceTests : IDisposable
{
    private readonly TempFolder _folder = new();
    private readonly PreferencesService _service;

    public PreferencesServiceTests()
    {
        var paths = Substitute.For<IAppPaths>();
        paths.PreferencesFile.Returns(_folder.File("preferences.json"));
        _service = new PreferencesService(new JsonFileStore(NullLogger<JsonFileStore>.Instance), paths, NullLogger<PreferencesService>.Instance);
    }

    [Fact]
    public async Task Applique_les_valeurs_par_defaut_sans_fichier()
    {
        Assert.True(await _service.LoadAsync(TestContext.Current.CancellationToken));

        Assert.True(_service.Current.UseWinget);
        Assert.True(_service.Current.SilentInstall);
        Assert.Equal(ThemeMode.System, _service.Current.Theme);
        Assert.Empty(_service.Current.Exclusions);
    }

    [Fact]
    public async Task Garde_les_valeurs_par_defaut_des_cles_absentes_du_fichier()
    {
        await File.WriteAllTextAsync(_folder.File("preferences.json"), """{"theme":"Dark"}""", TestContext.Current.CancellationToken);

        await _service.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ThemeMode.Dark, _service.Current.Theme);
        Assert.True(_service.Current.UseMsStore);
        Assert.Empty(_service.Current.Exclusions);
    }

    [Fact]
    public async Task Signale_un_fichier_illisible()
    {
        await File.WriteAllTextAsync(_folder.File("preferences.json"), "illisible", TestContext.Current.CancellationToken);

        Assert.False(await _service.LoadAsync(TestContext.Current.CancellationToken));
        Assert.True(_service.Current.UseWinget);
    }

    [Fact]
    public async Task N_ajoute_pas_deux_fois_la_meme_application_ignoree()
    {
        var steam = new ExclusionEntry("Valve.Steam", "Steam", "Ignorée depuis la liste", DateTimeOffset.UnixEpoch);

        await _service.AddExclusionsAsync([steam], TestContext.Current.CancellationToken);
        await _service.AddExclusionsAsync([steam with { Id = "valve.steam" }], TestContext.Current.CancellationToken);

        Assert.Single(_service.Current.Exclusions);
    }

    [Fact]
    public async Task Retire_une_application_ignoree_et_ecrit_le_fichier()
    {
        await _service.AddExclusionsAsync([new ExclusionEntry("Valve.Steam", "Steam", "r", DateTimeOffset.UnixEpoch)], TestContext.Current.CancellationToken);

        await _service.RemoveExclusionsAsync(["VALVE.STEAM"], TestContext.Current.CancellationToken);

        Assert.Empty(_service.Current.Exclusions);
        Assert.True(File.Exists(_folder.File("preferences.json")));
    }

    public void Dispose()
    {
        _service.Dispose();
        _folder.Dispose();
    }
}
