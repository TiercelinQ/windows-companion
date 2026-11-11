using System.ComponentModel;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using WindowsCompanion.Models;
using WindowsCompanion.Services;
using WindowsCompanion.Services.Platform;
using WindowsCompanion.Tests.Support;

namespace WindowsCompanion.Tests.Services;

public sealed class SystemInfoServiceTests
{
    private readonly ICimClient _cim = Substitute.For<ICimClient>();
    private readonly IRegistryReader _registry = Substitute.For<IRegistryReader>();

    private SystemInfoService Service()
    {
        var runner = Substitute.For<IProcessRunner>();
        runner.RunAsync(default!, default!, default, default).ThrowsAsyncForAnyArgs(new Win32Exception(2));
        var winget = Substitute.For<IWingetService>();
        winget.GetVersionAsync(default).ReturnsForAnyArgs(Result<string>.Success("1.12.350"));
        _registry.GetSubKeyNames(default, default!).ReturnsForAnyArgs([]);
        return new SystemInfoService(_cim, _registry, Substitute.For<INativeSystemInfo>(), runner, winget, TimeProvider.System, NullLogger<SystemInfoService>.Instance);
    }

    private void Answer(string table, params CimRecord[] rows) =>
        _cim.QueryAsync(Arg.Any<string>(), Arg.Is<string>(q => q.Contains(table, StringComparison.Ordinal)), Arg.Any<CancellationToken>())
            .Returns(rows);

    [Fact]
    public async Task Produit_les_six_cartes_dans_l_ordre()
    {
        var snapshot = await Service().GetSnapshotAsync(TestContext.Current.CancellationToken);

        Assert.Equal(Enum.GetValues<SystemGroupKind>(), snapshot.Groups.Select(g => g.Kind));
    }

    [Fact]
    public async Task Calcule_les_etats_de_securite()
    {
        Answer("MSFT_MpComputerStatus", Samples.Record(("RealTimeProtectionEnabled", true), ("AntivirusSignatureAge", 1u)));
        Answer("MSFT_NetFirewallProfile", Samples.Record(("Name", "Domain"), ("Enabled", (ushort)1)), Samples.Record(("Name", "Public"), ("Enabled", (ushort)0)));
        _cim.QueryAsync(CimScopes.BitLocker, Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(new ElevationRequiredException("bitlocker"));
        _registry.GetValue(RegistryRoot.LocalMachine, @"SYSTEM\CurrentControlSet\Control\SecureBoot\State", "UEFISecureBootEnabled").Returns(1);

        var snapshot = await Service().GetSnapshotAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new SecuritySummary(true, false, true, null), snapshot.Security);
        var bitLocker = snapshot.Groups.Single(g => g.Kind == SystemGroupKind.Security).Rows.Single(r => r.Label == "BitLocker");
        Assert.True(bitLocker.RequiresAdmin);
        Assert.Equal("Non disponible sans droits administrateur", bitLocker.DisplayValue);
    }

    [Fact]
    public async Task Indique_l_activation_de_windows()
    {
        Answer("SoftwareLicensingProduct", Samples.Record(("LicenseStatus", 1u)));

        var snapshot = await Service().GetSnapshotAsync(TestContext.Current.CancellationToken);

        var activation = snapshot.Groups[0].Rows.Single(r => r.Label == "Activation");
        Assert.Equal("Windows est activé", activation.Value);
        Assert.Equal(InfoState.Good, activation.State);
    }

    [Fact]
    public async Task Signale_les_pilotes_en_avertissement()
    {
        Answer("Win32_PnPEntity", Samples.Record(("DeviceID", "PCI\\1")));

        var snapshot = await Service().GetSnapshotAsync(TestContext.Current.CancellationToken);

        var warnings = snapshot.Groups.Single(g => g.Kind == SystemGroupKind.Drivers).Rows.Single(r => r.Label == "Pilotes avec avertissement");
        Assert.Equal(InfoState.Warning, warnings.State);
    }
}
