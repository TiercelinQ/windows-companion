namespace WindowsCompanion.Models;

/// <summary>Kind of network adapter, which drives its icon.</summary>
public enum AdapterKind
{
    /// <summary>Wired Ethernet.</summary>
    Ethernet,

    /// <summary>Wi-Fi.</summary>
    Wifi,

    /// <summary>VPN tunnel (WireGuard, OpenVPN...).</summary>
    Tunnel,

    /// <summary>Virtual switch (Hyper-V, WSL).</summary>
    Virtual,

    /// <summary>Any other adapter.</summary>
    Other,
}

/// <summary>Windows network profile.</summary>
public enum NetworkProfile
{
    /// <summary>Private.</summary>
    Private,

    /// <summary>Public.</summary>
    Public,

    /// <summary>Domain.</summary>
    Domain,

    /// <summary>Unidentified.</summary>
    Unidentified,
}

/// <summary>A network adapter.</summary>
/// <param name="Id">Interface identifier (GUID).</param>
/// <param name="Name">Name, for example "Ethernet".</param>
/// <param name="Description">Hardware description.</param>
/// <param name="Kind">Kind.</param>
/// <param name="IsUp">Whether the link is up.</param>
/// <param name="IsDisabled">Whether the adapter is administratively disabled.</param>
/// <param name="LinkSpeedBps">Link speed.</param>
/// <param name="IPv4">IPv4 addresses.</param>
/// <param name="Masks">Masks, aligned with <paramref name="IPv4"/>.</param>
/// <param name="Gateways">IPv4 gateways.</param>
/// <param name="DnsServers">DNS servers.</param>
/// <param name="DhcpServer">DHCP server, null when DHCP is off.</param>
/// <param name="LeaseExpires">DHCP lease expiry.</param>
/// <param name="IPv6LinkLocal">IPv6 link-local address.</param>
/// <param name="Mac">MAC address, formatted.</param>
/// <param name="Mtu">MTU.</param>
/// <param name="Profile">Network profile.</param>
/// <param name="BytesReceived">Bytes received since the adapter was enabled.</param>
/// <param name="BytesSent">Bytes sent since the adapter was enabled.</param>
public sealed record AdapterInfo(
    string Id,
    string Name,
    string Description,
    AdapterKind Kind,
    bool IsUp,
    bool IsDisabled,
    long? LinkSpeedBps,
    IReadOnlyList<string> IPv4,
    IReadOnlyList<string> Masks,
    IReadOnlyList<string> Gateways,
    IReadOnlyList<string> DnsServers,
    string? DhcpServer,
    DateTimeOffset? LeaseExpires,
    string? IPv6LinkLocal,
    string? Mac,
    int? Mtu,
    NetworkProfile? Profile,
    long? BytesReceived,
    long? BytesSent)
{
    /// <summary>Gets the first IPv4 gateway, or null.</summary>
    public string? Gateway => Gateways.Count > 0 ? Gateways[0] : null;

    /// <summary>Gets the first DNS server, or null.</summary>
    public string? PrimaryDns => DnsServers.Count > 0 ? DnsServers[0] : null;
}

/// <summary>Link state of the machine, shown by the overview tile (F-ACC-09).</summary>
public enum LinkStatus
{
    /// <summary>An active adapter has a gateway.</summary>
    Connected,

    /// <summary>An adapter is active without a gateway.</summary>
    Limited,

    /// <summary>No active adapter.</summary>
    Disconnected,
}

/// <summary>Network snapshot: the adapters at the time of the reading.</summary>
/// <param name="TakenAt">Time of the reading.</param>
/// <param name="Adapters">Adapters, connected first.</param>
public sealed record NetworkSnapshot(DateTimeOffset TakenAt, IReadOnlyList<AdapterInfo> Adapters)
{
    /// <summary>Gets the main adapter: the first connected one with a gateway, else the first connected one.</summary>
    public AdapterInfo? Primary =>
        Adapters.FirstOrDefault(a => a.IsUp && a.Gateways.Count > 0) ?? Adapters.FirstOrDefault(a => a.IsUp);

    /// <summary>Gets the link state.</summary>
    public LinkStatus Status => Primary switch
    {
        null => LinkStatus.Disconnected,
        { Gateways.Count: > 0 } => LinkStatus.Connected,
        _ => LinkStatus.Limited,
    };
}

/// <summary>The four local connectivity tests, in execution order.</summary>
public enum TestKind
{
    /// <summary>A connected adapter exists.</summary>
    Adapter,

    /// <summary>The gateway answers a ping.</summary>
    Gateway,

    /// <summary>A name resolves.</summary>
    Dns,

    /// <summary>The Windows connectivity probe answers.</summary>
    Internet,
}

/// <summary>State of a connectivity test.</summary>
public enum TestStatus
{
    /// <summary>Not run in this session.</summary>
    NotTested,

    /// <summary>Waiting for the previous tests.</summary>
    Waiting,

    /// <summary>Running.</summary>
    Running,

    /// <summary>Succeeded.</summary>
    Succeeded,

    /// <summary>Failed.</summary>
    Failed,
}

/// <summary>A connectivity test and its result.</summary>
/// <param name="Kind">Which test.</param>
/// <param name="Status">State.</param>
/// <param name="Detail">Detail line, set once the test ends.</param>
/// <param name="ElapsedMs">Measured duration, when relevant.</param>
public sealed record ConnectivityTest(TestKind Kind, TestStatus Status, string? Detail = null, long? ElapsedMs = null);

/// <summary>Global connectivity state shown by the chip of the Connectivity card (F-RES-03).</summary>
public enum ConnectivityState
{
    /// <summary>No test in this session.</summary>
    NotTested,

    /// <summary>All four tests succeeded.</summary>
    Connected,

    /// <summary>The gateway answers but Internet does not.</summary>
    InternetUnreachable,

    /// <summary>The gateway does not answer.</summary>
    LocalUnreachable,

    /// <summary>No connected adapter.</summary>
    NoNetwork,
}

/// <summary>Rules that derive the global state from the four tests.</summary>
public static class Connectivity
{
    /// <summary>Gets the global state of a completed series of tests.</summary>
    /// <param name="tests">The four tests.</param>
    /// <returns>The global state.</returns>
    public static ConnectivityState Evaluate(IReadOnlyList<ConnectivityTest> tests)
    {
        ArgumentNullException.ThrowIfNull(tests);
        TestStatus StatusOf(TestKind kind) => tests.FirstOrDefault(t => t.Kind == kind)?.Status ?? TestStatus.NotTested;

        if (tests.Count == 0 || tests.All(t => t.Status == TestStatus.NotTested))
        {
            return ConnectivityState.NotTested;
        }

        if (StatusOf(TestKind.Adapter) == TestStatus.Failed)
        {
            return ConnectivityState.NoNetwork;
        }

        if (StatusOf(TestKind.Gateway) == TestStatus.Failed)
        {
            return ConnectivityState.LocalUnreachable;
        }

        return tests.All(t => t.Status == TestStatus.Succeeded)
            ? ConnectivityState.Connected
            : ConnectivityState.InternetUnreachable;
    }
}
