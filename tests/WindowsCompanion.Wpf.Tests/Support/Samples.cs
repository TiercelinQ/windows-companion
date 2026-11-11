using WindowsCompanion.Models;
using WindowsCompanion.Services.Platform;

namespace WindowsCompanion.Tests.Support;

/// <summary>Demonstration data of the cahier (annexe B), shared by the tests.</summary>
internal static class Samples
{
    public const long Gib = 1024L * 1024 * 1024;
    public const long Mib = 1024L * 1024;

    public static UpdateItem Firefox { get; } = new("Mozilla.Firefox", "Mozilla Firefox", "148.0.2", "150.0", UpdateSource.Winget, 78 * Mib);

    public static UpdateItem Vscode { get; } = new("Microsoft.VisualStudioCode", "Visual Studio Code", "1.104.2", "1.106.0", UpdateSource.Winget, 96 * Mib);

    public static UpdateItem Spotify { get; } = new("9NCBCSZSJRSB", "Spotify", "1.2.70", "1.2.74", UpdateSource.MsStore, 128 * Mib);

    public static VolumeInfo Volume(string letter, long capacityGb, long usedGb, int disk = 0) =>
        new(disk, letter, "Samsung SSD 980 PRO 1 To", MediaKind.SsdNvme, "NTFS", capacityGb * Gib, usedGb * Gib, letter == "C:", DiskHealth.Good, 38);

    public static HardwareSnapshot Hardware(params VolumeInfo[] volumes) => new(
        DateTimeOffset.UnixEpoch,
        new CpuInfo("Intel Core i7-13700H", 14, 20, 2400, 1, null, null, null, true, 23),
        new MemoryInfo(34_000_000_000, 13_000_000_000, "DDR5", 5600, [new MemoryModule(16 * Gib, "Kingston"), new MemoryModule(16 * Gib, "Kingston")], 4),
        [new GpuInfo("NVIDIA GeForce RTX 4060", 8 * Gib, "581.42", null, "12_2")],
        [new DisplayInfo(2560, 1440)],
        new BoardInfo("ASUSTeK", "PRIME Z790-P", "AMI", "1802", null, FirmwareKind.Uefi, new TpmInfo(true, "2.0"), false),
        volumes,
        volumes.Select(v => v.DiskNumber).Distinct().Count());

    public static SystemSnapshot System(DateTimeOffset? boot = null, SecuritySummary? security = null) => new(
        DateTimeOffset.UnixEpoch,
        "DESKTOP-K7M2Q4",
        "Windows 11 Professionnel",
        "25H2",
        boot,
        [
            new(SystemGroupKind.OperatingSystem, [new("Nom", "Microsoft Windows 11 Professionnel"), new("Version", "25H2")]),
            new(SystemGroupKind.Security, [new("Pare-feu Windows", "Actif", InfoState.Good), new("BitLocker", null, RequiresAdmin: true)]),
        ],
        security ?? new SecuritySummary(true, true, true, null));

    public static AdapterInfo Ethernet(bool up = true, string? gateway = "192.168.1.1") => new(
        "{0B5C2B5A-8D47-4E1B-9C6E-2B3D4E5F6A7B}", "Ethernet", "Realtek PCIe 2.5GbE", AdapterKind.Ethernet, up, false, 1_000_000_000,
        ["192.168.1.42"], ["255.255.255.0"], gateway is null ? [] : [gateway], ["192.168.1.1"], "192.168.1.1", null,
        "fe80::5c2a:91ff:fe3b:7d10", "3C-52-82-1A-9F-04", 1500, NetworkProfile.Private, 1000, 2000);

    public static CimRecord Record(params (string Name, object? Value)[] values) =>
        new(values.ToDictionary(v => v.Name, v => v.Value, StringComparer.OrdinalIgnoreCase));
}
