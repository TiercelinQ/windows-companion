using System.Globalization;
using WindowsCompanion.Models;
using WindowsCompanion.Resources;

namespace WindowsCompanion.Helpers;

/// <summary>Label/value lines of the hardware and network blocks, shared by the pages and the HTML reports (A1).</summary>
public static class InfoRows
{
    private const string Separator = " / ";

    /// <summary>Gets the text of a line: the value, or why it is missing.</summary>
    /// <param name="row">The line.</param>
    /// <returns>The text.</returns>
    public static string ValueText(InfoRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.DisplayValue;
    }

    /// <summary>Gets the processor lines.</summary>
    /// <param name="cpu">Processor.</param>
    /// <returns>The lines.</returns>
    public static IReadOnlyList<InfoRow> Cpu(CpuInfo cpu)
    {
        ArgumentNullException.ThrowIfNull(cpu);
        return
        [
            new(Strings.Hw_Model, cpu.Model),
            new(Strings.Hw_CoresThreads, cpu.Cores is { } c && cpu.LogicalProcessors is { } t ? $"{Formats.Whole(c)}{Separator}{Formats.Whole(t)}" : null),
            new(Strings.Hw_BaseFrequency, cpu.BaseMHz is { } mhz ? Formats.Ghz(mhz) : null),
            new(Strings.Hw_MaxFrequency, null),
            new(Strings.Hw_Sockets, cpu.Sockets is { } s ? Formats.Whole(s) : null),
            new(Strings.Hw_Caches, cpu.CacheL1Bytes is null && cpu.CacheL2Bytes is null && cpu.CacheL3Bytes is null
                ? null
                : string.Join(Separator, new[] { cpu.CacheL1Bytes, cpu.CacheL2Bytes, cpu.CacheL3Bytes }.Select(b => b is { } bytes ? Formats.Size(bytes) : Strings.Common_Dash))),
            Flag(Strings.Hw_Virtualization, cpu.VirtualizationEnabled, Strings.State_Enabled, Strings.State_Disabled),
        ];
    }

    /// <summary>Gets the memory lines.</summary>
    /// <param name="memory">Memory.</param>
    /// <returns>The lines.</returns>
    public static IReadOnlyList<InfoRow> Memory(MemoryInfo memory)
    {
        ArgumentNullException.ThrowIfNull(memory);
        var modules = memory.Modules
            .GroupBy(m => (m.CapacityBytes, m.Manufacturer))
            .Select(g => Formats.Template(Strings.Hw_ModulesGroup, g.Count(), Formats.Gb(g.Key.CapacityBytes, 0)) + (g.Key.Manufacturer is { } maker ? $" ({maker})" : string.Empty));
        return
        [
            new(Strings.Hw_Installed, memory.InstalledBytes is { } installed ? Formats.Template(Strings.Size_Gb, Formats.Gb(installed, 0)) : null),
            new(Strings.Hw_TypeSpeed, memory.Type is null && memory.SpeedMTs is null
                ? null
                : string.Join(", ", new[] { memory.Type, memory.SpeedMTs is { } speed ? Formats.Template(Strings.Hw_SpeedMts, Formats.Whole(speed)) : null }.Where(p => p is not null))),
            new(Strings.Hw_Modules, memory.Modules.Count == 0 ? null : string.Join(", ", modules)),
            new(Strings.Hw_Slots, memory.SlotsTotal is { } slots && memory.Modules.Count > 0 ? Formats.Template(Strings.Hw_SlotsValue, memory.Modules.Count, slots) : null),
            new(Strings.Hw_Reserved, memory.ReservedBytes is { } reserved ? Formats.Size(reserved) : null),
        ];
    }

    /// <summary>Gets the lines of a graphics adapter.</summary>
    /// <param name="gpu">Graphics adapter.</param>
    /// <param name="displays">Connected displays.</param>
    /// <returns>The lines.</returns>
    public static IReadOnlyList<InfoRow> Gpu(GpuInfo gpu, IReadOnlyList<DisplayInfo> displays)
    {
        ArgumentNullException.ThrowIfNull(gpu);
        ArgumentNullException.ThrowIfNull(displays);
        var resolutions = displays.Select(d => Formats.Template(Strings.Hw_Resolution, Formats.Whole(d.Width), Formats.Whole(d.Height))).Distinct().ToList();
        return
        [
            new(Strings.Hw_Model, gpu.Model),
            new(Strings.Hw_DedicatedMemory, gpu.DedicatedMemoryBytes is { } vram and > 0 ? Formats.Template(Strings.Size_Gb, Formats.Gb(vram, 1)) : null),
            new(Strings.Hw_DriverVersion, gpu.DriverVersion, IsMono: true),
            new(Strings.Hw_DriverDate, gpu.DriverDate is { } date ? Formats.Date(date) : null),
            new(Strings.Hw_DirectX, gpu.DirectXLevel is { } level ? Formats.Template(Strings.Hw_DirectXValue, level.Split('_')[0], level) : null),
            new(Strings.Hw_Interface, null),
            new(Strings.Hw_Displays, displays.Count == 0 ? null : Formats.Template(Strings.Hw_DisplaysValue, displays.Count, string.Join(", ", resolutions))),
        ];
    }

    /// <summary>Gets the motherboard lines.</summary>
    /// <param name="board">Motherboard, BIOS, TPM.</param>
    /// <returns>The lines.</returns>
    public static IReadOnlyList<InfoRow> Board(BoardInfo board)
    {
        ArgumentNullException.ThrowIfNull(board);
        return
        [
            new(Strings.Hw_Manufacturer, board.Manufacturer),
            new(Strings.Hw_Model, board.Model),
            new(Strings.Hw_Chipset, null),
            new(Strings.Hw_Bios, board.BiosVendor is null && board.BiosVersion is null ? null : $"{board.BiosVendor} {board.BiosVersion}".Trim()),
            new(Strings.Hw_BiosDate, board.BiosDate is { } date ? Formats.Date(date) : null),
            new(Strings.Hw_BootMode, board.Firmware switch { FirmwareKind.Uefi => Strings.Firmware_Uefi, FirmwareKind.Bios => Strings.Firmware_Bios, _ => null }),
            new(Strings.Hw_Tpm, board.Tpm switch
            {
                null => null,
                { Present: true } tpm => Formats.Template(Strings.Hw_TpmPresent, tpm.Version ?? string.Empty),
                _ => Strings.Hw_TpmAbsent,
            }, board.Tpm?.Present == true ? InfoState.Good : InfoState.Neutral, board.TpmRequiresAdmin),
            new(Strings.Sys_SerialNumber, Strings.Common_Masked, IsMono: true),
        ];
    }

    /// <summary>Gets the detail lines of a volume.</summary>
    /// <param name="volume">Volume.</param>
    /// <returns>The lines.</returns>
    public static IReadOnlyList<InfoRow> Volume(VolumeInfo volume)
    {
        ArgumentNullException.ThrowIfNull(volume);
        return
        [
            new(Strings.Hw_FileSystem, volume.FileSystem),
            new(Strings.Hw_SystemDisk, volume.IsSystem ? Strings.Common_Yes : Strings.Common_No),
            new(Strings.Hw_Health, volume.Health switch
            {
                DiskHealth.Good => Strings.Health_Good,
                DiskHealth.Warning => Strings.Health_Warning,
                DiskHealth.Unhealthy => Strings.Health_Unhealthy,
                _ => Strings.Health_Unknown,
            }, volume.Health switch
            {
                DiskHealth.Good => InfoState.Good,
                DiskHealth.Warning or DiskHealth.Unhealthy => InfoState.Warning,
                _ => InfoState.Neutral,
            }),
            new(Strings.Hw_Temperature, volume.TemperatureC is { } t ? Formats.Template(Strings.Hw_TemperatureValue, t) : null),
        ];
    }

    /// <summary>Gets the title of a volume block: "Disque 0 (C:)".</summary>
    /// <param name="volume">Volume.</param>
    /// <returns>The title.</returns>
    public static string VolumeTitle(VolumeInfo volume)
    {
        ArgumentNullException.ThrowIfNull(volume);
        var disk = volume.DiskNumber >= 0 ? volume.DiskNumber.ToString(CultureInfo.InvariantCulture) : Strings.Common_Dash;
        return volume.DriveLetter is { } letter
            ? Formats.Template(Strings.Hw_DiskTitle, disk, letter)
            : Formats.Template(Strings.Hw_DiskTitleNoVolume, disk);
    }

    /// <summary>Gets the label of a medium.</summary>
    /// <param name="media">Medium.</param>
    /// <returns>For example "SSD NVMe".</returns>
    public static string MediaLabel(MediaKind media) => media switch
    {
        MediaKind.SsdNvme => Strings.Media_SsdNvme,
        MediaKind.SsdSata => Strings.Media_SsdSata,
        MediaKind.Hdd => Strings.Media_Hdd,
        MediaKind.HddUsb => Strings.Media_HddUsb,
        _ => Strings.Media_Other,
    };

    /// <summary>Gets the title of a System card.</summary>
    /// <param name="kind">Card.</param>
    /// <returns>The title.</returns>
    public static string GroupTitle(SystemGroupKind kind) => kind switch
    {
        SystemGroupKind.OperatingSystem => Strings.Sys_GroupOs,
        SystemGroupKind.Device => Strings.Sys_GroupDevice,
        SystemGroupKind.Security => Strings.Sys_GroupSecurity,
        SystemGroupKind.Session => Strings.Sys_GroupSession,
        SystemGroupKind.Software => Strings.Sys_GroupSoftware,
        _ => Strings.Sys_GroupDrivers,
    };

    /// <summary>Gets the lines of an adapter: 13 when connected, 4 otherwise (F-RES-10, F-RES-11).</summary>
    /// <param name="adapter">Adapter.</param>
    /// <param name="maskMac">Whether the MAC address is masked (exports, D7).</param>
    /// <returns>The lines.</returns>
    public static IReadOnlyList<InfoRow> Adapter(AdapterInfo adapter, bool maskMac)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        var mac = new InfoRow(Strings.Net_Mac, maskMac ? Strings.Common_Masked : adapter.Mac ?? Strings.Common_Dash, IsMono: true);
        var mtu = new InfoRow(Strings.Net_Mtu, adapter.Mtu is { } m ? m.ToString(CultureInfo.InvariantCulture) : Strings.Common_Dash);
        if (!adapter.IsUp)
        {
            return
            [
                new(Strings.Net_Description, adapter.Description),
                new(Strings.Net_State, adapter.IsDisabled ? Strings.Net_Disabled : Strings.Net_Disconnected, InfoState.Warning),
                mac,
                mtu,
            ];
        }

        return
        [
            new(Strings.Net_Description, adapter.Description),
            new(Strings.Net_LinkSpeed, Dash(Formats.LinkSpeed(adapter.LinkSpeedBps))),
            new(Strings.Net_IPv4, Lines(adapter.IPv4), IsMono: true),
            new(Strings.Net_Mask, Lines(adapter.Masks), IsMono: true),
            new(Strings.Net_Gateway, Lines(adapter.Gateways), IsMono: true),
            new(Strings.Net_Dns, Lines(adapter.DnsServers), IsMono: true),
            new(Strings.Net_Dhcp, adapter.DhcpServer is { } dhcp ? Formats.Template(Strings.Net_DhcpOn, dhcp) : Strings.Common_No),
            new(Strings.Net_Lease, adapter.LeaseExpires is { } lease ? Formats.DateTime(lease.LocalDateTime) : Strings.Common_Dash),
            new(Strings.Net_IPv6, adapter.IPv6LinkLocal ?? Strings.Common_Dash, IsMono: true),
            mac,
            mtu,
            new(Strings.Net_Profile, adapter.Profile switch
            {
                NetworkProfile.Private => Strings.Profile_Private,
                NetworkProfile.Public => Strings.Profile_Public,
                NetworkProfile.Domain => Strings.Profile_Domain,
                NetworkProfile.Unidentified => Strings.Profile_Unidentified,
                _ => Strings.Common_Dash,
            }),
            new(Strings.Net_Traffic, adapter.BytesReceived is { } received && adapter.BytesSent is { } sent
                ? Formats.Template(Strings.Net_TrafficValue, Formats.Gb(received, 1), Formats.Gb(sent, 1))
                : Strings.Common_Dash),
        ];
    }

    /// <summary>Gets the label of a history result.</summary>
    /// <param name="result">Result.</param>
    /// <returns>"Réussie", "Échec" or "Annulée".</returns>
    public static string HistoryResultLabel(HistoryResult result) => result switch
    {
        HistoryResult.Success => Strings.Result_Success,
        HistoryResult.Failure => Strings.Result_Failure,
        _ => Strings.Result_Cancelled,
    };

    /// <summary>Gets the short label of a source (table column).</summary>
    /// <param name="source">Source.</param>
    /// <returns>"winget" or "Store".</returns>
    public static string SourceLabel(UpdateSource source) => source == UpdateSource.MsStore ? Strings.Source_Store : Strings.Source_Winget;

    /// <summary>Gets the long label of a source (subtitle, toasts).</summary>
    /// <param name="source">Source.</param>
    /// <returns>"winget" or "Microsoft Store".</returns>
    public static string SourceLongLabel(UpdateSource source) => source == UpdateSource.MsStore ? Strings.Source_StoreLong : Strings.Source_Winget;

    private static InfoRow Flag(string label, bool? value, string on, string off) =>
        new(label, value is null ? null : value.Value ? on : off, value == true ? InfoState.Good : InfoState.Neutral);

    private static string Lines(IReadOnlyList<string> values) => values.Count == 0 ? Strings.Common_Dash : string.Join(Environment.NewLine, values);

    private static string Dash(string value) => value.Length == 0 ? Strings.Common_Dash : value;
}
