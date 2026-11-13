using System.IO;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WindowsCompanion.Models;
using WindowsCompanion.Services;
using WindowsCompanion.Tests.Support;

namespace WindowsCompanion.Tests.Services;

public sealed class ExportServiceTests : IDisposable
{
    private readonly TempFolder _documents = new();
    private readonly IProcessRunner _runner = Substitute.For<IProcessRunner>();
    private readonly ExportService _service;

    public ExportServiceTests()
    {
        var paths = Substitute.For<IAppPaths>();
        paths.Documents.Returns(_documents.Path);
        paths.Logs.Returns(Path.Combine(_documents.Path, "logs"));
        _service = new ExportService(paths, _runner, new ManualTimeProvider(), NullLogger<ExportService>.Instance);
    }

    [Fact]
    public void Ouvre_le_dossier_des_journaux_et_le_cree_s_il_manque()
    {
        var logs = Path.Combine(_documents.Path, "logs");

        _service.OpenLogsFolder();

        Assert.True(Directory.Exists(logs));
        _runner.Received(1).Start(
            Arg.Is<string>(f => f.EndsWith("explorer.exe", StringComparison.OrdinalIgnoreCase)),
            Arg.Is<IReadOnlyList<string>>(a => a.SequenceEqual(new[] { logs })));
    }

    [Fact]
    public async Task N_ecrase_jamais_un_rapport_existant()
    {
        var content = new ReportContent(Samples.Hardware(Samples.Volume("C:", 931, 612)), null, null, null);

        var first = await _service.ExportReportAsync(ExportKind.Hardware, content, TestContext.Current.CancellationToken);
        var second = await _service.ExportReportAsync(ExportKind.Hardware, content, TestContext.Current.CancellationToken);

        Assert.Equal("windows-companion-materiel-2026-11-08.html", Path.GetFileName(first.Data));
        Assert.Equal("windows-companion-materiel-2026-11-08-2.html", Path.GetFileName(second.Data));
    }

    [Fact]
    public async Task Produit_un_html_autonome_encode_et_sans_script()
    {
        var network = new NetworkSnapshot(DateTimeOffset.UnixEpoch, [Samples.Ethernet() with { Name = "<script>alert(1)</script>" }]);

        var result = await _service.ExportReportAsync(ExportKind.Network, new ReportContent(null, null, network, null), TestContext.Current.CancellationToken);
        var html = await File.ReadAllTextAsync(result.Data!, TestContext.Current.CancellationToken);

        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("3C-52-82-1A-9F-04", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ecrit_l_historique_en_csv_utf8_avec_bom_et_point_virgule()
    {
        var entry = new HistoryEntry(DateTimeOffset.UnixEpoch, "Docker.DockerDesktop", "Docker; Desktop", "4.44.1", "4.45.0", UpdateSource.Winget, HistoryResult.Failure, "1603", 190);

        var result = await _service.ExportHistoryCsvAsync([entry], TestContext.Current.CancellationToken);
        var bytes = await File.ReadAllBytesAsync(result.Data!, TestContext.Current.CancellationToken);
        var lines = Encoding.UTF8.GetString(bytes).Split(Environment.NewLine);

        Assert.Equal([0xEF, 0xBB, 0xBF], bytes.Take(3));
        Assert.StartsWith("﻿Date;Application;Identifiant;", lines[0], StringComparison.Ordinal);
        Assert.Contains("\"Docker; Desktop\";Docker.DockerDesktop;4.44.1;4.45.0;Échec;1603;190", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void Construit_le_resume_d_une_ligne()
    {
        var summary = _service.BuildSummary(Samples.Hardware(Samples.Volume("C:", 931, 612), Samples.Volume("D:", 1863, 1190, disk: 1)), Samples.System());

        Assert.Equal("DESKTOP-K7M2Q4 · Windows 11 Pro 25H2 · Intel Core i7-13700H · 32 Go · 2 disques", summary);
    }

    public void Dispose() => _documents.Dispose();
}
