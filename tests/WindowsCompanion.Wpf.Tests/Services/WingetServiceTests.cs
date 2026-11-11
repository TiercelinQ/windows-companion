using System.ComponentModel;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using WindowsCompanion.Models;
using WindowsCompanion.Services;
using WindowsCompanion.Tests.Support;

namespace WindowsCompanion.Tests.Services;

public sealed class WingetServiceTests
{
    private const string WingetTable = "Name      Id            Version Available Source\n--------------------------------------------------\nFirefox   Mozilla.FF    148.0   150.0     winget\n1 upgrades available.";

    private readonly IProcessRunner _runner = Substitute.For<IProcessRunner>();
    private readonly WingetService _service;
    private readonly List<WingetLogLine> _log = [];

    public WingetServiceTests() => _service = new WingetService(_runner, NullLogger<WingetService>.Instance);

    private void Answer(string sourceArgument, ProcessResult result) =>
        _runner.RunAsync(AppConfig.WingetExecutable, Arg.Is<IReadOnlyList<string>>(a => a.Contains(sourceArgument)), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
            .Returns(result);

    [Fact]
    public async Task Signale_winget_introuvable()
    {
        _runner.RunAsync(default!, default!, default, default).ReturnsForAnyArgs(Task.FromException<ProcessResult>(new Win32Exception(2)));

        var result = await _service.GetVersionAsync(TestContext.Current.CancellationToken);

        Assert.False(result.Ok);
    }

    [Fact]
    public async Task Garde_les_resultats_d_une_source_quand_l_autre_echoue()
    {
        Answer("winget", new ProcessResult(0, WingetTable, string.Empty));
        Answer("msstore", new ProcessResult(unchecked((int)0x8A150044), string.Empty, string.Empty));

        var result = await _service.SearchUpgradesAsync([UpdateSource.Winget, UpdateSource.MsStore], false, _log.Add, TestContext.Current.CancellationToken);

        Assert.True(result.Ok);
        Assert.Single(result.Data!.Items);
        Assert.Equal([UpdateSource.MsStore], result.Data.FailedSources);
        Assert.Contains(_log, l => l.Kind == LogLineKind.Command);
    }

    [Fact]
    public async Task Echoue_quand_aucune_source_ne_repond()
    {
        Answer("winget", new ProcessResult(5, string.Empty, string.Empty));

        var result = await _service.SearchUpgradesAsync([UpdateSource.Winget], false, _log.Add, TestContext.Current.CancellationToken);

        Assert.False(result.Ok);
        Assert.Equal(FeedbackType.Danger, result.Error!.Type);
    }

    [Fact]
    public async Task Rapporte_le_code_de_sortie_d_un_echec_d_installation()
    {
        _runner.RunAsync(default!, default!, default, default).ReturnsForAnyArgs(new ProcessResult(1603, string.Empty, string.Empty));

        var outcome = await _service.InstallAsync(Samples.Firefox, true, new Progress<InstallProgress>(), _log.Add, TestContext.Current.CancellationToken);

        Assert.Equal(new InstallOutcome(InstallResultKind.Failed, "1603"), outcome);
        Assert.Contains(_log, l => l.Kind == LogLineKind.Error);
    }

    [Fact]
    public async Task Traite_l_annulation_comme_une_annulation()
    {
        _runner.RunAsync(default!, default!, default, default).ThrowsAsyncForAnyArgs(new OperationCanceledException());

        var outcome = await _service.InstallAsync(Samples.Firefox, true, new Progress<InstallProgress>(), _log.Add, TestContext.Current.CancellationToken);

        Assert.Equal(InstallResultKind.Cancelled, outcome.Kind);
    }

    [Fact]
    public async Task Signale_le_passage_a_l_installation_apres_le_telechargement()
    {
        var phases = new List<InstallPhase>();
        var progress = new SyncProgress(p => phases.Add(p.Phase));
        _runner.RunAsync(default!, default!, default, default).ReturnsForAnyArgs(call =>
        {
            var onLine = call.ArgAt<Action<string>?>(2)!;
            onLine("  ██████▒▒▒  39.0 MB / 78.0 MB");
            onLine("  ██████████  78.0 MB / 78.0 MB");
            onLine("Installation réussie");
            return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
        });

        var outcome = await _service.InstallAsync(Samples.Firefox, true, progress, _log.Add, TestContext.Current.CancellationToken);

        Assert.Equal(InstallResultKind.Installed, outcome.Kind);
        Assert.Equal(InstallPhase.Installing, phases[^1]);
        Assert.Contains(_log, l => l.Text == "Installation réussie");
    }

    private sealed class SyncProgress(Action<InstallProgress> report) : IProgress<InstallProgress>
    {
        public void Report(InstallProgress value) => report(value);
    }
}
