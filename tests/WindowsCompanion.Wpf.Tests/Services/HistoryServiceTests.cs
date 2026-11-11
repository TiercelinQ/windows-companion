using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WindowsCompanion.Models;
using WindowsCompanion.Services;
using WindowsCompanion.Services.Data;
using WindowsCompanion.Tests.Support;

namespace WindowsCompanion.Tests.Services;

public sealed class HistoryServiceTests : IDisposable
{
    private readonly TempFolder _folder = new();
    private readonly HistoryService _service;

    public HistoryServiceTests()
    {
        var paths = Substitute.For<IAppPaths>();
        paths.HistoryFile.Returns(_folder.File("history.json"));
        _service = new HistoryService(new JsonFileStore(NullLogger<JsonFileStore>.Instance), paths, NullLogger<HistoryService>.Instance);
    }

    private static HistoryEntry Entry(string id) =>
        new(DateTimeOffset.UnixEpoch, id, id, "1.0", "2.0", UpdateSource.Winget, HistoryResult.Success, null, 10);

    [Fact]
    public async Task Place_la_derniere_entree_en_tete()
    {
        await _service.AddAsync(Entry("a"), TestContext.Current.CancellationToken);
        await _service.AddAsync(Entry("b"), TestContext.Current.CancellationToken);

        var load = await _service.ListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["b", "a"], load.Entries.Select(e => e.AppId));
    }

    [Fact]
    public async Task Limite_l_historique_a_mille_entrees()
    {
        for (var i = 0; i <= AppConfig.HistoryMaxEntries; i++)
        {
            await _service.AddAsync(Entry(i.ToString(System.Globalization.CultureInfo.InvariantCulture)), TestContext.Current.CancellationToken);
        }

        var load = await _service.ListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AppConfig.HistoryMaxEntries, load.Entries.Count);
        Assert.Equal("1000", load.Entries[0].AppId);
    }

    [Fact]
    public async Task Vide_l_historique()
    {
        await _service.AddAsync(Entry("a"), TestContext.Current.CancellationToken);

        await _service.ClearAsync(TestContext.Current.CancellationToken);

        Assert.Empty((await _service.ListAsync(TestContext.Current.CancellationToken)).Entries);
    }

    [Fact]
    public async Task Signale_une_seule_fois_un_fichier_illisible()
    {
        await File.WriteAllTextAsync(_folder.File("history.json"), "[", TestContext.Current.CancellationToken);

        Assert.True((await _service.ListAsync(TestContext.Current.CancellationToken)).WasReset);
        Assert.False((await _service.ListAsync(TestContext.Current.CancellationToken)).WasReset);
    }

    public void Dispose()
    {
        _service.Dispose();
        _folder.Dispose();
    }
}
