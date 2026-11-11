using NSubstitute;
using WindowsCompanion.Models;
using WindowsCompanion.Services;
using WindowsCompanion.Services.Platform;
using WindowsCompanion.Tests.Support;

namespace WindowsCompanion.Tests.Services;

public sealed class RunningAppServiceTests
{
    private readonly IRegistryReader _registry = Substitute.For<IRegistryReader>();
    private readonly IProcessRunner _processes = Substitute.For<IProcessRunner>();
    private readonly RunningAppService _service;

    public RunningAppServiceTests()
    {
        var key = RegistryPaths.Uninstall[0];
        _registry.GetSubKeyNames(key.Root, key.Path).Returns(["Firefox"]);
        _registry.GetValue(key.Root, key.Path + "\\Firefox", "DisplayName").Returns("Mozilla Firefox (x64 fr)");
        _registry.GetValue(key.Root, key.Path + "\\Firefox", "InstallLocation").Returns(@"C:\Program Files\Mozilla Firefox");
        _registry.GetSubKeyNames(Arg.Is<RegistryRoot>(r => r != key.Root), Arg.Any<string>()).Returns([]);
        _service = new RunningAppService(_registry, _processes);
    }

    [Fact]
    public async Task Detecte_une_application_lancee_depuis_son_dossier_d_installation()
    {
        _processes.GetProcesses().Returns([new RunningProcess(10, @"C:\Program Files\Mozilla Firefox\firefox.exe"), new RunningProcess(11, null)]);

        Assert.True(await _service.IsRunningAsync(Samples.Firefox, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Ignore_un_dossier_au_nom_voisin()
    {
        _processes.GetProcesses().Returns([new RunningProcess(10, @"C:\Program Files\Mozilla Firefox Beta\firefox.exe")]);

        Assert.False(await _service.IsRunningAsync(Samples.Firefox, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Ferme_chaque_processus_de_l_application()
    {
        _processes.GetProcesses().Returns([new RunningProcess(10, @"C:\Program Files\Mozilla Firefox\firefox.exe")]);
        _processes.CloseAsync(10, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);

        Assert.True(await _service.CloseAsync(Samples.Firefox, TestContext.Current.CancellationToken));
        await _processes.Received(1).CloseAsync(10, TimeSpan.FromMilliseconds(AppConfig.CloseAppWaitMs), Arg.Any<CancellationToken>());
    }
}
