using System.Net.NetworkInformation;
using WindowsCompanion.Models;
using WindowsCompanion.Services;
using WindowsCompanion.Tests.Support;

namespace WindowsCompanion.Tests.Services;

public sealed class NetworkServiceTests
{
    [Theory]
    [InlineData(NetworkInterfaceType.Ethernet, "Ethernet", "Realtek PCIe 2.5GbE", AdapterKind.Ethernet)]
    [InlineData(NetworkInterfaceType.Wireless80211, "Wi-Fi", "Intel Wi-Fi 6E AX211", AdapterKind.Wifi)]
    [InlineData(NetworkInterfaceType.Unknown, "WireGuard", "WireGuard Tunnel", AdapterKind.Tunnel)]
    [InlineData(NetworkInterfaceType.Ethernet, "vEthernet (WSL)", "Hyper-V Virtual Ethernet Adapter", AdapterKind.Virtual)]
    public void Classe_les_adaptateurs(NetworkInterfaceType type, string name, string description, AdapterKind expected) =>
        Assert.Equal(expected, NetworkService.Classify(type, name, description));

    [Theory]
    [InlineData(NetworkInterfaceType.Loopback, "Software Loopback Interface 1")]
    [InlineData(NetworkInterfaceType.Tunnel, "Teredo Tunneling Pseudo-Interface")]
    [InlineData(NetworkInterfaceType.Ethernet, "Realtek PCIe 2.5GbE-QoS Packet Scheduler-0000")]
    public void Masque_les_interfaces_d_infrastructure(NetworkInterfaceType type, string description) =>
        Assert.True(NetworkService.IsHidden(type, description));

    [Fact]
    public void Place_les_adaptateurs_connectes_en_premier()
    {
        var wifi = Samples.Ethernet(up: false) with { Name = "Wi-Fi", Kind = AdapterKind.Wifi };
        var virtualSwitch = Samples.Ethernet() with { Name = "vEthernet (WSL)", Kind = AdapterKind.Virtual };

        var ordered = NetworkService.Order([wifi, virtualSwitch, Samples.Ethernet()]);

        Assert.Equal(["Ethernet", "vEthernet (WSL)", "Wi-Fi"], ordered.Select(a => a.Name));
    }

    [Fact]
    public void Deduit_l_etat_de_connexion_de_l_adaptateur_principal()
    {
        Assert.Equal(LinkStatus.Connected, new NetworkSnapshot(DateTimeOffset.UnixEpoch, [Samples.Ethernet()]).Status);
        Assert.Equal(LinkStatus.Limited, new NetworkSnapshot(DateTimeOffset.UnixEpoch, [Samples.Ethernet(gateway: null)]).Status);
        Assert.Equal(LinkStatus.Disconnected, new NetworkSnapshot(DateTimeOffset.UnixEpoch, [Samples.Ethernet(up: false)]).Status);
    }

    [Fact]
    public void Calcule_l_etat_de_connectivite_des_quatre_tests()
    {
        ConnectivityTest Test(TestKind kind, TestStatus status) => new(kind, status);

        Assert.Equal(ConnectivityState.NotTested, Connectivity.Evaluate([Test(TestKind.Adapter, TestStatus.NotTested)]));
        Assert.Equal(ConnectivityState.Connected, Connectivity.Evaluate([.. Enum.GetValues<TestKind>().Select(k => Test(k, TestStatus.Succeeded))]));
        Assert.Equal(ConnectivityState.InternetUnreachable, Connectivity.Evaluate(
            [Test(TestKind.Adapter, TestStatus.Succeeded), Test(TestKind.Gateway, TestStatus.Succeeded), Test(TestKind.Dns, TestStatus.Succeeded), Test(TestKind.Internet, TestStatus.Failed)]));
        Assert.Equal(ConnectivityState.LocalUnreachable, Connectivity.Evaluate([Test(TestKind.Adapter, TestStatus.Succeeded), Test(TestKind.Gateway, TestStatus.Failed)]));
        Assert.Equal(ConnectivityState.NoNetwork, Connectivity.Evaluate([Test(TestKind.Adapter, TestStatus.Failed)]));
    }
}
