using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using WindowsCompanion.Models;
using WindowsCompanion.Services.Data;
using WindowsCompanion.Tests.Support;

namespace WindowsCompanion.Tests.Services.Data;

public sealed class JsonFileStoreTests : IDisposable
{
    private readonly TempFolder _folder = new();
    private readonly JsonFileStore _store = new(NullLogger<JsonFileStore>.Instance);

    [Fact]
    public async Task Renvoie_null_quand_le_fichier_est_absent()
    {
        var read = await _store.ReadAsync(_folder.File("absent.json"), AppJsonContext.Default.AppSettings, TestContext.Current.CancellationToken);

        Assert.Null(read);
    }

    [Fact]
    public async Task Relit_ce_qu_il_a_ecrit_sans_laisser_de_fichier_temporaire()
    {
        var path = _folder.File("preferences.json");
        await _store.WriteAsync(path, new AppSettings { Theme = ThemeMode.Dark }, AppJsonContext.Default.AppSettings, TestContext.Current.CancellationToken);

        var read = await _store.ReadAsync(path, AppJsonContext.Default.AppSettings, TestContext.Current.CancellationToken);

        Assert.Equal(ThemeMode.Dark, read!.Theme);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public async Task Met_de_cote_un_fichier_illisible()
    {
        var path = _folder.File("history.json");
        await File.WriteAllTextAsync(path, "{ pas du json", TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<StoreCorruptedException>(() =>
            _store.ReadAsync(path, AppJsonContext.Default.HistoryDocument, TestContext.Current.CancellationToken));

        Assert.False(File.Exists(path));
        Assert.True(File.Exists(path + ".bak"));
    }

    public void Dispose() => _folder.Dispose();
}
