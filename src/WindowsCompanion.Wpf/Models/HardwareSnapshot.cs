namespace WindowsCompanion.Models;

/// <summary>Kind of storage medium (cahier 5.5).</summary>
public enum MediaKind
{
    /// <summary>NVMe solid-state drive.</summary>
    SsdNvme,

    /// <summary>SATA solid-state drive.</summary>
    SsdSata,

    /// <summary>Internal hard disk.</summary>
    Hdd,

    /// <summary>USB hard disk.</summary>
    HddUsb,

    /// <summary>Any other medium.</summary>
    Other,
}

/// <summary>Health reported by Windows for a physical disk (not raw SMART attributes, cahier E4).</summary>
public enum DiskHealth
{
    /// <summary>Healthy.</summary>
    Good,

    /// <summary>Warning.</summary>
    Warning,

    /// <summary>Unhealthy.</summary>
    Unhealthy,

    /// <summary>Not reported.</summary>
    Unknown,
}

/// <summary>Firmware boot mode.</summary>
public enum FirmwareKind
{
    /// <summary>UEFI.</summary>
    Uefi,

    /// <summary>Legacy BIOS.</summary>
    Bios,
}

/// <summary>Hardware snapshot taken at the last analysis; never refreshed continuously.</summary>
/// <param name="TakenAt">Time of the analysis.</param>
/// <param name="Cpu">Processor.</param>
/// <param name="Memory">Memory.</param>
/// <param name="Gpus">Physical graphics adapters (virtual and basic adapters excluded).</param>
/// <param name="Displays">Connected displays.</param>
/// <param name="Board">Motherboard, BIOS, TPM.</param>
/// <param name="Volumes">Volumes of fixed disks, and disks without a mounted volume.</param>
/// <param name="PhysicalDiskCount">Number of physical disks.</param>
public sealed record HardwareSnapshot(
    DateTimeOffset TakenAt,
    CpuInfo Cpu,
    MemoryInfo Memory,
    IReadOnlyList<GpuInfo> Gpus,
    IReadOnlyList<DisplayInfo> Displays,
    BoardInfo Board,
    IReadOnlyList<VolumeInfo> Volumes,
    int PhysicalDiskCount)
{
    /// <summary>Gets the number of detected components: processor, memory, graphics adapters, motherboard, disks (F-MAT-02).</summary>
    public int ComponentCount => 3 + Gpus.Count + PhysicalDiskCount;
}

/// <summary>Processor information.</summary>
/// <param name="Model">Model name.</param>
/// <param name="Cores">Physical cores.</param>
/// <param name="LogicalProcessors">Threads.</param>
/// <param name="BaseMHz">Base frequency.</param>
/// <param name="Sockets">Number of sockets.</param>
/// <param name="CacheL1Bytes">L1 cache size.</param>
/// <param name="CacheL2Bytes">L2 cache size.</param>
/// <param name="CacheL3Bytes">L3 cache size.</param>
/// <param name="VirtualizationEnabled">Firmware virtualization state.</param>
/// <param name="UsagePercent">Usage sampled over one second during the analysis.</param>
public sealed record CpuInfo(
    string? Model,
    int? Cores,
    int? LogicalProcessors,
    double? BaseMHz,
    int? Sockets,
    long? CacheL1Bytes,
    long? CacheL2Bytes,
    long? CacheL3Bytes,
    bool? VirtualizationEnabled,
    double? UsagePercent);

/// <summary>A memory module.</summary>
/// <param name="CapacityBytes">Capacity.</param>
/// <param name="Manufacturer">Manufacturer.</param>
public sealed record MemoryModule(long CapacityBytes, string? Manufacturer);

/// <summary>Memory information.</summary>
/// <param name="VisibleBytes">Physical memory visible to Windows.</param>
/// <param name="AvailableBytes">Available memory.</param>
/// <param name="Type">Memory type (DDR4, DDR5).</param>
/// <param name="SpeedMTs">Configured speed, in MT/s.</param>
/// <param name="Modules">Installed modules.</param>
/// <param name="SlotsTotal">Number of slots.</param>
public sealed record MemoryInfo(
    long VisibleBytes,
    long AvailableBytes,
    string? Type,
    int? SpeedMTs,
    IReadOnlyList<MemoryModule> Modules,
    int? SlotsTotal)
{
    /// <summary>Gets the memory in use.</summary>
    public long UsedBytes => Math.Max(0, VisibleBytes - AvailableBytes);

    /// <summary>Gets the installed capacity, sum of the modules; null when no module is reported.</summary>
    public long? InstalledBytes => Modules.Count == 0 ? null : Modules.Sum(m => m.CapacityBytes);

    /// <summary>Gets the memory reserved for hardware: installed minus visible (approximate).</summary>
    public long? ReservedBytes => InstalledBytes is { } installed && installed > VisibleBytes ? installed - VisibleBytes : null;

    /// <summary>Gets the share of visible memory in use.</summary>
    public double UsagePercent => VisibleBytes == 0 ? 0 : (double)UsedBytes / VisibleBytes * 100;
}

/// <summary>A physical graphics adapter.</summary>
/// <param name="Model">Model name.</param>
/// <param name="DedicatedMemoryBytes">Dedicated video memory (DXGI).</param>
/// <param name="DriverVersion">Driver version.</param>
/// <param name="DriverDate">Driver date.</param>
/// <param name="DirectXLevel">Highest Direct3D 12 feature level, for example <c>12_2</c>.</param>
public sealed record GpuInfo(
    string Model,
    long? DedicatedMemoryBytes,
    string? DriverVersion,
    DateTime? DriverDate,
    string? DirectXLevel);

/// <summary>A connected display.</summary>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
public sealed record DisplayInfo(int Width, int Height);

/// <summary>TPM state.</summary>
/// <param name="Present">Whether a TPM is present and enabled.</param>
/// <param name="Version">Specification version, for example <c>2.0</c>.</param>
public sealed record TpmInfo(bool Present, string? Version);

/// <summary>Motherboard, BIOS and TPM.</summary>
/// <param name="Manufacturer">Board manufacturer.</param>
/// <param name="Model">Board model.</param>
/// <param name="BiosVendor">BIOS vendor.</param>
/// <param name="BiosVersion">BIOS version.</param>
/// <param name="BiosDate">BIOS release date.</param>
/// <param name="Firmware">Boot mode, null when unknown.</param>
/// <param name="Tpm">TPM, null when unreadable.</param>
/// <param name="TpmRequiresAdmin">Whether the TPM could not be read without administrator rights.</param>
public sealed record BoardInfo(
    string? Manufacturer,
    string? Model,
    string? BiosVendor,
    string? BiosVersion,
    DateTime? BiosDate,
    FirmwareKind? Firmware,
    TpmInfo? Tpm,
    bool TpmRequiresAdmin);

/// <summary>A volume of a fixed disk, or a disk without a mounted volume (no drive letter).</summary>
/// <param name="DiskNumber">Physical disk number.</param>
/// <param name="DriveLetter">Drive letter with its colon (<c>C:</c>), null for a disk without a mounted volume.</param>
/// <param name="DiskModel">Disk model.</param>
/// <param name="Media">Kind of medium.</param>
/// <param name="FileSystem">File system.</param>
/// <param name="CapacityBytes">Volume capacity, or disk size without a volume.</param>
/// <param name="UsedBytes">Used space.</param>
/// <param name="IsSystem">Whether the volume holds Windows.</param>
/// <param name="Health">Health of the disk.</param>
/// <param name="TemperatureC">Disk temperature, null when not reported.</param>
public sealed record VolumeInfo(
    int DiskNumber,
    string? DriveLetter,
    string DiskModel,
    MediaKind Media,
    string? FileSystem,
    long CapacityBytes,
    long UsedBytes,
    bool IsSystem,
    DiskHealth Health,
    int? TemperatureC)
{
    /// <summary>Gets the free space.</summary>
    public long FreeBytes => Math.Max(0, CapacityBytes - UsedBytes);

    /// <summary>Gets the used share, not rounded (thresholds apply before rounding, F-MAT-14).</summary>
    public double UsedPercent => CapacityBytes == 0 ? 0 : (double)UsedBytes / CapacityBytes * 100;

    /// <summary>Gets the usage level.</summary>
    public UsageLevel Level => UsageLevels.From(UsedPercent);
}
