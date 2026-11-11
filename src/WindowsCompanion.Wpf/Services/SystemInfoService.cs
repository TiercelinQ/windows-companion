using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.CSharp.RuntimeBinder;
using Microsoft.Extensions.Logging;
using WindowsCompanion.Helpers;
using WindowsCompanion.Models;
using WindowsCompanion.Resources;
using WindowsCompanion.Services.Platform;

namespace WindowsCompanion.Services;

/// <summary>Reads the system snapshot: the six cards of the System page (cahier 4.5).</summary>
public interface ISystemInfoService
{
    /// <summary>Takes a snapshot. Unreadable values stay null; the call itself does not fail.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The snapshot.</returns>
    Task<SystemSnapshot> GetSnapshotAsync(CancellationToken ct);
}

/// <summary>CIM, registry and command-line implementation of <see cref="ISystemInfoService"/>.</summary>
/// <param name="cim">CIM queries.</param>
/// <param name="registry">Registry reads.</param>
/// <param name="native">Windows API values.</param>
/// <param name="runner">Runs <c>dotnet --list-runtimes</c>.</param>
/// <param name="winget">Reads the winget version.</param>
/// <param name="time">Clock.</param>
/// <param name="logger">Logger.</param>
public sealed partial class SystemInfoService(
    ICimClient cim,
    IRegistryReader registry,
    INativeSystemInfo native,
    IProcessRunner runner,
    IWingetService winget,
    TimeProvider time,
    ILogger<SystemInfoService> logger) : ISystemInfoService
{
    private const string CurrentVersionKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
    private const string WebView2Client = "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";
    private const string WindowsApplicationId = "55c92734-d682-4d71-983e-d6ec3f16059f";
    private const int ProtectionOn = 1;
    private const int LicensedStatus = 1;
    private const int MaxSignatureAgeDays = 7;

    /// <inheritdoc />
    public async Task<SystemSnapshot> GetSnapshotAsync(CancellationToken ct)
    {
        var os = Query(CimScopes.Cimv2, "SELECT Caption, OSArchitecture, InstallDate, LastBootUpTime FROM Win32_OperatingSystem", ct);
        var computer = Query(CimScopes.Cimv2, "SELECT Name, Manufacturer, Model, SystemType, Workgroup, Domain, PartOfDomain FROM Win32_ComputerSystem", ct);
        var defender = Query(CimScopes.Defender, "SELECT RealTimeProtectionEnabled, AntivirusSignatureAge FROM MSFT_MpComputerStatus", ct);
        var firewall = Query(CimScopes.StandardCimv2, "SELECT Name, Enabled FROM MSFT_NetFirewallProfile", ct);
        var problems = Query(CimScopes.Cimv2, "SELECT DeviceID FROM Win32_PnPEntity WHERE ConfigManagerErrorCode <> 0", ct);
        var drivers = Query(CimScopes.Cimv2, "SELECT DeviceID FROM Win32_PnPSignedDriver", ct);
        var services = Query(CimScopes.Cimv2, "SELECT Name FROM Win32_Service WHERE State = 'Running'", ct);
        var bitLocker = QueryAdmin(CimScopes.BitLocker, "SELECT DriveLetter, ProtectionStatus FROM Win32_EncryptableVolume", ct);
        var restorePoints = QueryAdmin(CimScopes.Default, "SELECT CreationTime FROM SystemRestore", ct);
        var tpm = HardwareService.ReadTpmAsync(cim, ct);
        var runtimes = DotnetRuntimesAsync(ct);
        var wingetVersion = winget.GetVersionAsync(ct);
        var temp = Task.Run(TempFolderBytes, ct);
        var lastUpdateCheck = Task.Run(LastUpdateSearch, ct);

        await Task.WhenAll(os, computer, defender, firewall, problems, drivers, services, bitLocker, restorePoints, tpm, runtimes, wingetVersion, temp, lastUpdateCheck).ConfigureAwait(false);

        // The licensing provider answers with no row when queried alongside the other providers: read it on its own.
        var licensing = await Query(CimScopes.Cimv2, $"SELECT LicenseStatus FROM SoftwareLicensingProduct WHERE ApplicationID = '{WindowsApplicationId}' AND PartialProductKey IS NOT NULL", ct).ConfigureAwait(false);

        var osRecord = CimRecord.FirstOf(await os.ConfigureAwait(false));
        var csRecord = CimRecord.FirstOf(await computer.ConfigureAwait(false));
        var caption = osRecord?.Text("Caption");
        var edition = caption?.Replace("Microsoft ", string.Empty, StringComparison.Ordinal) ?? Strings.Common_NotAvailable;
        var displayVersion = registry.GetValue(RegistryRoot.LocalMachine, CurrentVersionKey, "DisplayVersion") as string;
        var bootTime = osRecord?.Date("LastBootUpTime") is { } boot ? new DateTimeOffset(boot) : (DateTimeOffset?)null;
        var deviceName = csRecord?.Text("Name") ?? Environment.MachineName;

        var security = Security(
            await defender.ConfigureAwait(false),
            await firewall.ConfigureAwait(false),
            await bitLocker.ConfigureAwait(false),
            await tpm.ConfigureAwait(false));

        var groups = new List<SystemGroup>
        {
            new(SystemGroupKind.OperatingSystem,
            [
                new(Strings.Sys_Name, caption),
                new(Strings.Sys_Version, displayVersion),
                new(Strings.Sys_Build, Build()),
                new(Strings.Sys_Architecture, osRecord?.Text("OSArchitecture")),
                new(Strings.Sys_InstalledOn, osRecord?.Date("InstallDate") is { } installed ? Formats.Date(installed) : null),
                new(Strings.Sys_Language, Capitalize(CultureInfo.InstalledUICulture.DisplayName)),
                Activation(licensing),
            ]),
            new(SystemGroupKind.Device,
            [
                new(Strings.Sys_DeviceName, deviceName),
                new(Strings.Sys_Manufacturer, csRecord?.Text("Manufacturer")),
                new(Strings.Sys_SystemType, SystemType(csRecord?.Text("SystemType"))),
                new(csRecord?.Flag("PartOfDomain") == true ? Strings.Sys_Domain : Strings.Sys_Workgroup, csRecord?.Flag("PartOfDomain") == true ? csRecord.Text("Domain") : csRecord?.Text("Workgroup")),
                new(Strings.Sys_SerialNumber, Strings.Common_Masked, IsMono: true),
                new(Strings.Sys_TimeZone, TimeZoneInfo.Local.DisplayName),
            ]),
            new(SystemGroupKind.Security, security.Rows),
            new(SystemGroupKind.Session,
            [
                new(Strings.Sys_User, $"{Environment.UserDomainName}\\{Environment.UserName}"),
                new(Strings.Sys_BootedOn, bootTime is { } b ? Formats.DateTime(b.LocalDateTime) : null),
                new(Strings.Sys_Uptime, bootTime is { } since ? Formats.Uptime(time.GetLocalNow() - since) : null),
                OnOff(Strings.Sys_FastStartup, ReadFlag(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled"), healthy: null),
                new(Strings.Sys_BiosMode, native.GetFirmwareType() switch { FirmwareKind.Uefi => Strings.Firmware_Uefi, FirmwareKind.Bios => Strings.Firmware_Bios, _ => null }),
                new(Strings.Sys_LastUpdateCheck, await lastUpdateCheck.ConfigureAwait(false) is { } checkedAt ? Formats.Date(checkedAt) : null),
            ]),
            new(SystemGroupKind.Software,
            [
                new(Strings.Sys_DotnetRuntimes, await runtimes.ConfigureAwait(false)),
                new(Strings.Sys_WindowsPowerShell, registry.GetValue(RegistryRoot.LocalMachine, @"SOFTWARE\Microsoft\PowerShell\3\PowerShellEngine", "PowerShellVersion") as string),
                new(Strings.Sys_PowerShell, PowerShell7()),
                new(Strings.Sys_WebView2, WebView2()),
                new(Strings.Sys_Winget, (await wingetVersion.ConfigureAwait(false)) is { Ok: true, Data: { } v } ? "v" + v : null),
                new(Strings.Sys_InstalledApps, Formats.Whole(InstalledApplications())),
            ]),
            new(SystemGroupKind.Drivers,
            [
                new(Strings.Sys_Drivers, Count(await drivers.ConfigureAwait(false))),
                Problems(await problems.ConfigureAwait(false)),
                new(Strings.Sys_RunningServices, Count(await services.ConfigureAwait(false))),
                RestorePoints(await restorePoints.ConfigureAwait(false)),
                new(Strings.Sys_TempFolder, await temp.ConfigureAwait(false) is { } bytes ? Formats.Size(bytes) : null),
            ]),
        };

        return new SystemSnapshot(time.GetLocalNow(), deviceName, edition, displayVersion, bootTime, groups, security.Summary);
    }

    private static string? Count(IReadOnlyList<CimRecord>? records) => records is null ? null : Formats.Whole(records.Count);

    private static InfoRow OnOff(string label, bool? value, bool? healthy) => new(
        label,
        value is null ? null : value.Value ? Strings.State_On : Strings.State_Off,
        value is not null && healthy is not null && value == healthy ? InfoState.Good : InfoState.Neutral);

    private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpper(text[0], CultureInfo.CurrentCulture) + text[1..];

    private static string? SystemType(string? raw) => raw switch
    {
        null => null,
        _ when raw.Contains("ARM64", StringComparison.OrdinalIgnoreCase) => Strings.Sys_SystemTypeArm64,
        _ when raw.Contains("x64", StringComparison.OrdinalIgnoreCase) => Strings.Sys_SystemTypeX64,
        _ => raw,
    };

    private static InfoRow Activation(IReadOnlyList<CimRecord>? licensing)
    {
        if (licensing is null || licensing.Count == 0)
        {
            return new(Strings.Sys_Activation, null);
        }

        var activated = licensing.Any(l => l.Number("LicenseStatus") == LicensedStatus);
        return new(Strings.Sys_Activation, activated ? Strings.Sys_Activated : Strings.Sys_NotActivated, activated ? InfoState.Good : InfoState.Warning);
    }

    private static InfoRow Problems(IReadOnlyList<CimRecord>? problems) => problems is null
        ? new(Strings.Sys_DriverWarnings, null)
        : new(Strings.Sys_DriverWarnings, Formats.Whole(problems.Count), problems.Count > 0 ? InfoState.Warning : InfoState.Neutral);

    private static InfoRow RestorePoints(AdminQuery points)
    {
        if (points.RequiresAdmin || points.Records is null)
        {
            return new(Strings.Sys_RestorePoints, null, RequiresAdmin: points.RequiresAdmin);
        }

        var last = points.Records.Select(r => r.Date("CreationTime")).Where(d => d is not null).Max();
        var value = last is null
            ? Formats.Whole(points.Records.Count)
            : Formats.Template(Strings.Sys_RestorePointsValue, Formats.Whole(points.Records.Count), Formats.Date(last.Value));
        return new(Strings.Sys_RestorePoints, value);
    }

    private static long? TempFolderBytes()
    {
        try
        {
            var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
            return new DirectoryInfo(Path.GetTempPath()).EnumerateFiles("*", options).Sum(f => f.Length);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // Windows Update Agent, late-bound COM: no type library reference, "not available" on any failure.
    private static DateTime? LastUpdateSearch()
    {
        object? agent = null;
        try
        {
            var type = Type.GetTypeFromProgID("Microsoft.Update.AutoUpdate");
            agent = type is null ? null : Activator.CreateInstance(type);
            if (agent is null)
            {
                return null;
            }

            dynamic autoUpdate = agent;
            DateTime date = autoUpdate.Results.LastSearchSuccessDate;
            return date.Year < 2000 ? null : date.ToLocalTime();
        }
        catch (Exception ex) when (ex is COMException or RuntimeBinderException or InvalidCastException or TargetInvocationException)
        {
            return null;
        }
        finally
        {
            if (agent is not null && Marshal.IsComObject(agent))
            {
                Marshal.FinalReleaseComObject(agent);
            }
        }
    }

    [GeneratedRegex(@"Microsoft\.NETCore\.App (?<major>\d+)\.(?<minor>\d+)\.")]
    private static partial Regex RuntimeLine();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Requete CIM en echec : {Query}")]
    private static partial void LogQueryFailed(ILogger logger, string query, Exception exception);

    private (SecuritySummary Summary, IReadOnlyList<InfoRow> Rows) Security(
        IReadOnlyList<CimRecord>? defender,
        IReadOnlyList<CimRecord>? firewall,
        AdminQuery bitLocker,
        (TpmInfo? Tpm, bool RequiresAdmin) tpm)
    {
        var secureBoot = ReadFlag(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State", "UEFISecureBootEnabled");
        var uac = ReadFlag(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableLUA");

        bool? defenderOn = CimRecord.FirstOf(defender)?.Flag("RealTimeProtectionEnabled");
        var signatureAge = CimRecord.FirstOf(defender)?.Number("AntivirusSignatureAge");
        var defenderText = defenderOn switch
        {
            null => null,
            false => Strings.State_Off,
            true when signatureAge is <= MaxSignatureAgeDays => Strings.Sys_DefenderUpToDate,
            true => Strings.Sys_DefenderOutdated,
        };

        // MSFT_NetFirewallProfile.Enabled: 0 = off, 1 = on, 2 = not configured (default: on).
        var profiles = firewall ?? [];
        var disabledProfiles = profiles.Where(p => p.Number("Enabled") == 0).Select(p => p.Text("Name")).ToList();
        bool? firewallOn = profiles.Count == 0 ? null : disabledProfiles.Count == 0;
        var firewallText = firewallOn switch
        {
            null => null,
            true => Strings.State_Active,
            false => Formats.Template(Strings.Sys_FirewallOff, string.Join(", ", disabledProfiles)),
        };

        var systemLetter = Path.GetPathRoot(Environment.SystemDirectory)?.TrimEnd('\\');
        bool? bitLockerSystem = null;
        string? bitLockerText = null;
        if (bitLocker.Records is { } volumes)
        {
            bitLockerSystem = volumes.Any(v => string.Equals(v.Text("DriveLetter"), systemLetter, StringComparison.OrdinalIgnoreCase) && v.Number("ProtectionStatus") == ProtectionOn);
            bitLockerText = string.Join(", ", volumes
                .Where(v => v.Text("DriveLetter") is not null)
                .Select(v => Formats.Template(v.Number("ProtectionStatus") == ProtectionOn ? Strings.Sys_BitLockerOn : Strings.Sys_BitLockerOff, v.Text("DriveLetter")!)));
        }

        var tpmText = tpm.Tpm switch
        {
            null => null,
            { Present: true } t => Formats.Template(Strings.Hw_TpmPresent, t.Version ?? string.Empty),
            _ => Strings.Hw_TpmAbsent,
        };

        var rows = new List<InfoRow>
        {
            OnOff(Strings.Sys_SecureBoot, secureBoot, healthy: true),
            new(Strings.Hw_Tpm, tpmText, tpm.Tpm?.Present == true ? InfoState.Good : InfoState.Neutral, tpm.RequiresAdmin),
            new(Strings.Sys_BitLocker, bitLockerText, bitLockerSystem == true ? InfoState.Good : InfoState.Neutral, bitLocker.RequiresAdmin),
            new(Strings.Sys_Defender, defenderText, defenderOn == true && signatureAge is <= MaxSignatureAgeDays ? InfoState.Good : InfoState.Neutral),
            new(Strings.Sys_Firewall, firewallText, firewallOn == true ? InfoState.Good : InfoState.Neutral),
            OnOff(Strings.Sys_Uac, uac, healthy: true),
        };

        return (new SecuritySummary(defenderOn, firewallOn, secureBoot, bitLockerSystem), rows);
    }

    private bool? ReadFlag(string key, string name) =>
        registry.GetValue(RegistryRoot.LocalMachine, key, name) is int value ? value != 0 : null;

    private string? Build()
    {
        var build = registry.GetValue(RegistryRoot.LocalMachine, CurrentVersionKey, "CurrentBuild") as string;
        var revision = registry.GetValue(RegistryRoot.LocalMachine, CurrentVersionKey, "UBR");
        return build is null ? null : revision is int ubr ? $"{build}.{ubr.ToString(CultureInfo.InvariantCulture)}" : build;
    }

    private string? PowerShell7()
    {
        const string Key = @"SOFTWARE\Microsoft\PowerShellCore\InstalledVersions";
        return registry.GetSubKeyNames(RegistryRoot.LocalMachine, Key)
            .Select(sub => registry.GetValue(RegistryRoot.LocalMachine, Key + "\\" + sub, "SemanticVersion") as string)
            .Where(v => v is not null)
            .OrderByDescending(v => Version.TryParse(v!.Split('-')[0], out var parsed) ? parsed : new Version())
            .FirstOrDefault() ?? Strings.Sys_NotInstalled;
    }

    private string? WebView2()
    {
        var version = registry.GetValue(RegistryRoot.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\" + WebView2Client, "pv") as string
            ?? registry.GetValue(RegistryRoot.CurrentUser, @"Software\Microsoft\EdgeUpdate\Clients\" + WebView2Client, "pv") as string;
        return version is null or "0.0.0.0" ? Strings.Sys_NotInstalled : Formats.Template(Strings.Sys_InstalledVersion, version);
    }

    private int InstalledApplications()
    {
        var count = 0;
        foreach (var key in RegistryPaths.Uninstall)
        {
            foreach (var sub in registry.GetSubKeyNames(key.Root, key.Path))
            {
                var path = key.Path + "\\" + sub;
                if (registry.GetValue(key.Root, path, "DisplayName") is string { Length: > 0 }
                    && registry.GetValue(key.Root, path, "SystemComponent") is not 1
                    && registry.GetValue(key.Root, path, "ParentKeyName") is null)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private async Task<string?> DotnetRuntimesAsync(CancellationToken ct)
    {
        try
        {
            var result = await runner.RunAsync("dotnet", ["--list-runtimes"], null, ct).ConfigureAwait(false);
            var versions = RuntimeLine().Matches(result.StandardOutput)
                .Select(m => (Major: int.Parse(m.Groups["major"].Value, CultureInfo.InvariantCulture), Minor: int.Parse(m.Groups["minor"].Value, CultureInfo.InvariantCulture)))
                .Distinct()
                .OrderBy(v => v.Major)
                .ThenBy(v => v.Minor)
                .Select(v => $"{v.Major.ToString(CultureInfo.InvariantCulture)}.{v.Minor.ToString(CultureInfo.InvariantCulture)}.x")
                .ToList();
            return versions.Count == 0 ? null : string.Join(", ", versions);
        }
        catch (Win32Exception)
        {
            return null; // no dotnet host on the machine
        }
    }

    private async Task<IReadOnlyList<CimRecord>?> Query(string scope, string wql, CancellationToken ct)
    {
        try
        {
            return await cim.QueryAsync(scope, wql, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is CimQueryException or ElevationRequiredException)
        {
            LogQueryFailed(logger, wql, ex);
            return null;
        }
    }

    private async Task<AdminQuery> QueryAdmin(string scope, string wql, CancellationToken ct)
    {
        try
        {
            return new AdminQuery(await cim.QueryAsync(scope, wql, ct).ConfigureAwait(false), false);
        }
        catch (ElevationRequiredException)
        {
            return new AdminQuery(null, true);
        }
        catch (CimQueryException ex)
        {
            LogQueryFailed(logger, wql, ex);
            return new AdminQuery(null, false);
        }
    }

    private sealed record AdminQuery(IReadOnlyList<CimRecord>? Records, bool RequiresAdmin);
}
