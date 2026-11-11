using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using WindowsCompanion.Models;
using WindowsCompanion.Services;
using WindowsCompanion.Services.Platform;
using WindowsCompanion.Tests.Support;

namespace WindowsCompanion.Tests.Services;

public sealed class HardwareServiceTests
{
    [Fact]
    public void Exclut_les_cartes_graphiques_virtuelles_et_ajoute_le_pilote()
    {
        var adapters = new[] { new GpuAdapter("NVIDIA GeForce RTX 4060", 8 * Samples.Gib, "12_2"), new GpuAdapter("Microsoft Remote Display Adapter", 0, null) };
        var controllers = new[] { Samples.Record(("Name", "NVIDIA GeForce RTX 4060"), ("DriverVersion", "32.0.15.8142")) };

        var gpus = HardwareService.Gpus(adapters, controllers);

        var gpu = Assert.Single(gpus);
        Assert.Equal("32.0.15.8142", gpu.DriverVersion);
        Assert.Equal(8 * Samples.Gib, gpu.DedicatedMemoryBytes);
    }

    [Fact]
    public void Se_rabat_sur_cim_quand_dxgi_ne_repond_pas()
    {
        var gpus = HardwareService.Gpus([], [Samples.Record(("Name", "Intel UHD Graphics")), Samples.Record(("Name", "Microsoft Basic Display Adapter"))]);

        Assert.Equal("Intel UHD Graphics", Assert.Single(gpus).Model);
    }

    [Fact]
    public async Task Produit_un_instantane_meme_quand_les_requetes_echouent()
    {
        var cim = Substitute.For<ICimClient>();
        cim.QueryAsync(default!, default!, default).ThrowsAsyncForAnyArgs(new CimQueryException("indisponible"));
        var native = Substitute.For<INativeSystemInfo>();
        native.GetMemoryStatus().Returns(new MemoryStatus(32 * Samples.Gib, 16 * Samples.Gib));
        native.SampleCpuUsageAsync(default, default).ReturnsForAnyArgs(23.0);
        native.GetDisplays().Returns([]);
        var dxgi = Substitute.For<IDxgiAdapterReader>();
        dxgi.GetAdapters().Returns([]);
        var service = new HardwareService(cim, native, dxgi, TimeProvider.System, NullLogger<HardwareService>.Instance);

        var snapshot = await service.GetSnapshotAsync(TestContext.Current.CancellationToken);

        Assert.Null(snapshot.Cpu.Model);
        Assert.Equal(23, snapshot.Cpu.UsagePercent);
        Assert.Equal(50, snapshot.Memory.UsagePercent);
        Assert.Null(snapshot.Board.Tpm);
    }

    [Fact]
    public async Task Signale_un_tpm_reserve_aux_administrateurs()
    {
        var cim = Substitute.For<ICimClient>();
        cim.QueryAsync(CimScopes.Tpm, Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(new ElevationRequiredException("tpm"));

        var (tpm, requiresAdmin) = await HardwareService.ReadTpmAsync(cim, TestContext.Current.CancellationToken);

        Assert.Null(tpm);
        Assert.True(requiresAdmin);
    }
}
