using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using WindowsCompanion.Helpers;
using WindowsCompanion.Models;
using WindowsCompanion.Resources;
using WindowsCompanion.Services.Platform;

namespace WindowsCompanion.Services;

/// <summary>Network adapters, connectivity tests and the two explicit network actions (cahier 4.6).</summary>
public interface INetworkService
{
    /// <summary>Reads the adapters, connected first (F-RES-08).</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The snapshot.</returns>
    Task<NetworkSnapshot> GetAdaptersAsync(CancellationToken ct);

    /// <summary>Runs the four local tests in order; every test runs even when an earlier one failed (F-RES-07).</summary>
    /// <param name="snapshot">Adapters read just before.</param>
    /// <param name="progress">Receives each test when it starts and when it ends.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The four results.</returns>
    Task<IReadOnlyList<ConnectivityTest>> RunConnectivityTestsAsync(NetworkSnapshot snapshot, IProgress<ConnectivityTest> progress, CancellationToken ct);

    /// <summary>Enables a disabled adapter (administrator).</summary>
    /// <param name="adapter">The adapter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or a failure worded for the user (N-24).</returns>
    Task<Result<Unit>> EnableAdapterAsync(AdapterInfo adapter, CancellationToken ct);

    /// <summary>Clears the DNS client cache.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or a failure worded for the user (N-24).</returns>
    Task<Result<Unit>> FlushDnsAsync(CancellationToken ct);
}

/// <summary><c>System.Net</c> and CIM implementation of <see cref="INetworkService"/>.</summary>
/// <param name="cim">CIM queries and the adapter method call.</param>
/// <param name="runner">Runs <c>ipconfig</c>.</param>
/// <param name="http">Shared HTTP client of the Internet probe.</param>
/// <param name="time">Clock.</param>
/// <param name="logger">Logger.</param>
public sealed partial class NetworkService(
    ICimClient cim,
    IProcessRunner runner,
    HttpClient http,
    TimeProvider time,
    ILogger<NetworkService> logger) : INetworkService
{
    private const int NetAdapterDisabled = 3;

    // Infrastructure interfaces never shown (F-RES-08).
    private static readonly string[] HiddenMarkers = ["Teredo", "ISATAP", "6to4", "IP-HTTPS", "Kernel Debug", "WAN Miniport", "Wi-Fi Direct", "Loopback", "Packet Scheduler", "WFP ", "Lightweight Filter", "Native WiFi Filter", "Virtual WiFi Filter"];
    private static readonly string[] TunnelMarkers = ["WireGuard", "VPN", "OpenVPN", "TAP-", "Tunnel", "Tailscale", "ZeroTier"];
    private static readonly string[] VirtualMarkers = ["Hyper-V", "vEthernet", "VirtualBox", "VMware", "Virtual"];

    /// <summary>Gets the kind of an adapter from its interface type and names.</summary>
    /// <param name="type">Interface type.</param>
    /// <param name="name">Adapter name.</param>
    /// <param name="description">Hardware description.</param>
    /// <returns>The kind.</returns>
    public static AdapterKind Classify(NetworkInterfaceType type, string name, string description)
    {
        bool Has(string[] markers) => markers.Any(m => name.Contains(m, StringComparison.OrdinalIgnoreCase) || description.Contains(m, StringComparison.OrdinalIgnoreCase));
        return type switch
        {
            _ when Has(TunnelMarkers) => AdapterKind.Tunnel,
            _ when Has(VirtualMarkers) => AdapterKind.Virtual,
            NetworkInterfaceType.Wireless80211 => AdapterKind.Wifi,
            NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet or NetworkInterfaceType.FastEthernetT or NetworkInterfaceType.FastEthernetFx or NetworkInterfaceType.Ethernet3Megabit => AdapterKind.Ethernet,
            _ => AdapterKind.Other,
        };
    }

    /// <summary>Tells whether an interface is infrastructure and stays hidden.</summary>
    /// <param name="type">Interface type.</param>
    /// <param name="description">Hardware description.</param>
    /// <returns>True when hidden.</returns>
    public static bool IsHidden(NetworkInterfaceType type, string description) =>
        type == NetworkInterfaceType.Loopback || HiddenMarkers.Any(m => description.Contains(m, StringComparison.OrdinalIgnoreCase));

    /// <summary>Orders the adapters: connected first, then Ethernet, Wi-Fi, tunnels, virtual, others.</summary>
    /// <param name="adapters">Adapters.</param>
    /// <returns>The ordered list.</returns>
    public static IReadOnlyList<AdapterInfo> Order(IEnumerable<AdapterInfo> adapters) =>
        [.. adapters.OrderByDescending(a => a.IsUp).ThenBy(a => a.Kind).ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)];

    /// <inheritdoc />
    public async Task<NetworkSnapshot> GetAdaptersAsync(CancellationToken ct)
    {
        var interfaces = Task.Run(ReadInterfaces, ct);
        var leases = Query(CimScopes.Cimv2, "SELECT SettingID, DHCPLeaseExpires FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = TRUE", ct);
        var profiles = Query(CimScopes.StandardCimv2, "SELECT InterfaceAlias, NetworkCategory FROM MSFT_NetConnectionProfile", ct);
        var netAdapters = Query(CimScopes.StandardCimv2, "SELECT Name, InterfaceDescription, InterfaceGuid, PermanentAddress, MtuSize, State FROM MSFT_NetAdapter", ct);
        await Task.WhenAll(interfaces, leases, profiles, netAdapters).ConfigureAwait(false);

        var leaseById = (await leases.ConfigureAwait(false))
            .Where(r => r.Text("SettingID") is not null)
            .ToDictionary(r => r.Text("SettingID")!, r => r.Date("DHCPLeaseExpires"), StringComparer.OrdinalIgnoreCase);
        var profileByAlias = (await profiles.ConfigureAwait(false))
            .Where(r => r.Text("InterfaceAlias") is not null)
            .GroupBy(r => r.Text("InterfaceAlias")!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => Profile(g.First().Number("NetworkCategory")), StringComparer.OrdinalIgnoreCase);

        var adapters = (await interfaces.ConfigureAwait(false))
            .Select(a => a with
            {
                LeaseExpires = leaseById.TryGetValue(a.Id, out var lease) && lease is { } l ? new DateTimeOffset(l) : null,
                Profile = profileByAlias.TryGetValue(a.Name, out var profile) ? profile : null,
            })
            .ToList();

        // Disabled adapters are absent from NetworkInterface: read them through CIM (A5).
        foreach (var disabled in (await netAdapters.ConfigureAwait(false)).Where(r => r.Number("State") == NetAdapterDisabled))
        {
            var id = disabled.Text("InterfaceGuid") ?? string.Empty;
            var description = disabled.Text("InterfaceDescription") ?? string.Empty;
            if (adapters.Exists(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase)) || IsHidden(NetworkInterfaceType.Unknown, description))
            {
                continue;
            }

            var name = disabled.Text("Name") ?? description;
            adapters.Add(new AdapterInfo(
                id, name, description, Classify(NetworkInterfaceType.Unknown, name, description), false, true, null,
                [], [], [], [], null, null, null, Formats.Mac(disabled.Text("PermanentAddress")), (int?)disabled.Number("MtuSize"), null, null, null));
        }

        return new NetworkSnapshot(time.GetLocalNow(), Order(adapters));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConnectivityTest>> RunConnectivityTestsAsync(NetworkSnapshot snapshot, IProgress<ConnectivityTest> progress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(progress);
        var primary = snapshot.Primary;
        var gateway = primary?.Gateway;
        var results = new List<ConnectivityTest>();

        async Task Run(TestKind kind, Func<Task<ConnectivityTest>> test)
        {
            progress.Report(new ConnectivityTest(kind, TestStatus.Running));
            var result = await test().ConfigureAwait(false);
            results.Add(result);
            progress.Report(result);
        }

        await Run(TestKind.Adapter, () => Task.FromResult(primary is null
            ? new ConnectivityTest(TestKind.Adapter, TestStatus.Failed, Strings.Net_TestAdapterFailed)
            : new ConnectivityTest(TestKind.Adapter, TestStatus.Succeeded, Formats.Template(Strings.Net_TestAdapterOk, primary.Name, Formats.LinkSpeed(primary.LinkSpeedBps))))).ConfigureAwait(false);
        await Run(TestKind.Gateway, () => PingGatewayAsync(gateway, ct)).ConfigureAwait(false);
        await Run(TestKind.Dns, () => ResolveAsync(primary?.PrimaryDns, ct)).ConfigureAwait(false);
        await Run(TestKind.Internet, () => ProbeInternetAsync(ct)).ConfigureAwait(false);
        return results;
    }

    /// <inheritdoc />
    public async Task<Result<Unit>> EnableAdapterAsync(AdapterInfo adapter, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        var operation = Formats.Template(Strings.Net_EnableOperation, adapter.Name);

        // The identifier comes from Windows itself; parsed and re-formatted so the query only ever holds a GUID.
        if (!Guid.TryParse(adapter.Id, out var guid))
        {
            return Failed(operation, Strings.Net_InvalidAdapter);
        }

        try
        {
            var code = await cim.InvokeAsync(CimScopes.StandardCimv2, $"SELECT * FROM MSFT_NetAdapter WHERE InterfaceGuid = '{guid:B}'", "Enable", ct).ConfigureAwait(false);
            return code == 0 ? Result<Unit>.Success(Unit.Value) : Failed(operation, Formats.Template(Strings.Winget_ExitCodeCause, code));
        }
        catch (ElevationRequiredException)
        {
            return Result<Unit>.Failure(new(FeedbackType.Warning, Strings.Toast_AdminRequired_Title, Formats.Template(Strings.Toast_AdminRequired_Message, operation)));
        }
        catch (CimQueryException ex)
        {
            LogActionFailed(logger, operation, ex);
            return Failed(operation, Strings.Net_CimFailed);
        }
    }

    /// <inheritdoc />
    public async Task<Result<Unit>> FlushDnsAsync(CancellationToken ct)
    {
        try
        {
            var result = await runner.RunAsync(Path.Combine(Environment.SystemDirectory, "ipconfig.exe"), ["/flushdns"], null, ct).ConfigureAwait(false);
            return result.ExitCode == 0
                ? Result<Unit>.Success(Unit.Value)
                : Failed(Strings.Net_FlushOperation, Formats.Template(Strings.Winget_ExitCodeCause, result.ExitCode));
        }
        catch (Win32Exception ex)
        {
            LogActionFailed(logger, Strings.Net_FlushOperation, ex);
            return Failed(Strings.Net_FlushOperation, Strings.Net_IpconfigMissing);
        }
    }

    private static Result<Unit> Failed(string operation, string cause) =>
        Result<Unit>.Failure(new(FeedbackType.Danger, Strings.Toast_OperationFailed_Title, Formats.Template(Strings.Toast_OperationFailed_Message, operation, cause)));

    private static NetworkProfile? Profile(long? category) => category switch
    {
        0 => NetworkProfile.Public,
        1 => NetworkProfile.Private,
        2 => NetworkProfile.Domain,
        _ => NetworkProfile.Unidentified,
    };

    private static List<AdapterInfo> ReadInterfaces()
    {
        var adapters = new List<AdapterInfo>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (IsHidden(nic.NetworkInterfaceType, nic.Description))
            {
                continue;
            }

            var properties = nic.GetIPProperties();
            var v4 = properties.UnicastAddresses.Where(u => u.Address.AddressFamily == AddressFamily.InterNetwork).ToList();
            IPv4InterfaceProperties? ipv4 = null;
            try
            {
                ipv4 = properties.GetIPv4Properties();
            }
            catch (NetworkInformationException)
            {
                // No IPv4 stack on this interface.
            }

            IPInterfaceStatistics? stats = null;
            try
            {
                stats = nic.GetIPStatistics();
            }
            catch (NetworkInformationException)
            {
                // Statistics not available.
            }

            var isUp = nic.OperationalStatus == OperationalStatus.Up;
            adapters.Add(new AdapterInfo(
                nic.Id,
                nic.Name,
                nic.Description,
                Classify(nic.NetworkInterfaceType, nic.Name, nic.Description),
                isUp,
                false,
                isUp && nic.Speed > 0 ? nic.Speed : null,
                [.. v4.Select(u => u.Address.ToString())],
                [.. v4.Select(u => u.IPv4Mask.ToString())],
                [.. properties.GatewayAddresses.Where(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any)).Select(g => g.Address.ToString())],
                [.. properties.DnsAddresses.OrderBy(d => d.AddressFamily == AddressFamily.InterNetwork ? 0 : 1).Select(d => d.ToString())],
                ipv4?.IsDhcpEnabled == true ? properties.DhcpServerAddresses.FirstOrDefault()?.ToString() : null,
                null,
                properties.UnicastAddresses.FirstOrDefault(u => u.Address.IsIPv6LinkLocal)?.Address.ToString().Split('%')[0],
                Formats.Mac(nic.GetPhysicalAddress().ToString()),
                ipv4?.Mtu,
                null,
                stats?.BytesReceived,
                stats?.BytesSent));
        }

        return adapters;
    }

    private static async Task<ConnectivityTest> PingGatewayAsync(string? gateway, CancellationToken ct)
    {
        if (gateway is null || !IPAddress.TryParse(gateway, out var address))
        {
            return new ConnectivityTest(TestKind.Gateway, TestStatus.Failed, Strings.Net_TestNoGateway);
        }

        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(address, TimeSpan.FromMilliseconds(AppConfig.GatewayPingTimeoutMs), null, null, ct).ConfigureAwait(false);
            return reply.Status == IPStatus.Success
                ? new ConnectivityTest(TestKind.Gateway, TestStatus.Succeeded, Formats.Template(Strings.Net_TestGatewayOk, gateway), reply.RoundtripTime)
                : new ConnectivityTest(TestKind.Gateway, TestStatus.Failed, Formats.Template(Strings.Net_TestGatewayFailed, gateway));
        }
        catch (PingException)
        {
            return new ConnectivityTest(TestKind.Gateway, TestStatus.Failed, Formats.Template(Strings.Net_TestGatewayFailed, gateway));
        }
    }

    private static async Task<ConnectivityTest> ResolveAsync(string? dnsServer, CancellationToken ct)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(AppConfig.DnsTimeoutMs);
        var watch = Stopwatch.StartNew();
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(AppConfig.DnsTestHost, limit.Token).ConfigureAwait(false);
            return addresses.Length > 0
                ? new ConnectivityTest(TestKind.Dns, TestStatus.Succeeded, Formats.Template(Strings.Net_TestDnsOk, AppConfig.DnsTestHost, dnsServer ?? Strings.Common_NotAvailable), watch.ElapsedMilliseconds)
                : new ConnectivityTest(TestKind.Dns, TestStatus.Failed, Formats.Template(Strings.Net_TestDnsFailed, AppConfig.DnsTestHost));
        }
        catch (Exception ex) when (ex is SocketException || (ex is OperationCanceledException && !ct.IsCancellationRequested))
        {
            return new ConnectivityTest(TestKind.Dns, TestStatus.Failed, Formats.Template(Strings.Net_TestDnsFailed, AppConfig.DnsTestHost));
        }
    }

    private async Task<ConnectivityTest> ProbeInternetAsync(CancellationToken ct)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(AppConfig.InternetTimeoutMs);
        var watch = Stopwatch.StartNew();
        try
        {
            var body = await http.GetStringAsync(new Uri(AppConfig.InternetTestUrl), limit.Token).ConfigureAwait(false);
            return body.Trim() == AppConfig.InternetTestExpected
                ? new ConnectivityTest(TestKind.Internet, TestStatus.Succeeded, Strings.Net_TestInternetOk, watch.ElapsedMilliseconds)
                : new ConnectivityTest(TestKind.Internet, TestStatus.Failed, Strings.Net_TestInternetFailed);
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is OperationCanceledException && !ct.IsCancellationRequested))
        {
            return new ConnectivityTest(TestKind.Internet, TestStatus.Failed, Strings.Net_TestInternetFailed);
        }
    }

    private async Task<IReadOnlyList<CimRecord>> Query(string scope, string wql, CancellationToken ct)
    {
        try
        {
            return await cim.QueryAsync(scope, wql, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is CimQueryException or ElevationRequiredException)
        {
            LogActionFailed(logger, wql, ex);
            return [];
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Operation reseau en echec : {Operation}")]
    private static partial void LogActionFailed(ILogger logger, string operation, Exception exception);
}
