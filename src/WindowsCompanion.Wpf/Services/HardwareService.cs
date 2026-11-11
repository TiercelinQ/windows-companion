using System.IO;
using Microsoft.Extensions.Logging;
using WindowsCompanion.Models;
using WindowsCompanion.Services.Platform;

namespace WindowsCompanion.Services;

/// <summary>Reads the hardware snapshot (cahier 4.4, T-04).</summary>
public interface IHardwareService
{
    /// <summary>Takes a snapshot. Unreadable values stay null (F-MAT-06); the call itself does not fail.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The snapshot.</returns>
    Task<HardwareSnapshot> GetSnapshotAsync(CancellationToken ct);
}

/// <summary>CIM, Windows API and DXGI implementation of <see cref="IHardwareService"/>.</summary>
/// <param name="cim">CIM queries.</param>
/// <param name="native">Windows API values.</param>
/// <param name="dxgi">Graphics adapters.</param>
/// <param name="time">Clock.</param>
/// <param name="logger">Logger.</param>
public sealed partial class HardwareService(
    ICimClient cim,
    INativeSystemInfo native,
    IDxgiAdapterReader dxgi,
    TimeProvider time,
    ILogger<HardwareService> logger) : IHardwareService
{
    private const long KiB = 1024;
    private const int BusUsb = 7;
    private const int BusSata = 11;
    private const int BusNvme = 17;
    private const int MediaHdd = 3;
    private const int MediaSsd = 4;

    // Virtual, remote and basic display adapters are not physical cards (F-LIM-08).
    private static readonly string[] VirtualAdapterMarkers = ["Microsoft Basic", "Microsoft Remote", "Remote Display", "Hyper-V", "Virtual", "Parsec", "Citrix"];

    /// <summary>Reads the TPM; shared with the system snapshot.</summary>
    /// <param name="cim">CIM queries.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The TPM (null when unreadable) and whether the reading needs administrator rights.</returns>
    public static async Task<(TpmInfo? Tpm, bool RequiresAdmin)> ReadTpmAsync(ICimClient cim, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cim);
        try
        {
            var records = await cim.QueryAsync(CimScopes.Tpm, "SELECT IsEnabled_InitialValue, SpecVersion FROM Win32_Tpm", ct).ConfigureAwait(false);
            if (records.Count == 0)
            {
                return (new TpmInfo(false, null), false);
            }

            var version = records[0].Text("SpecVersion")?.Split(',')[0].Trim();
            return (new TpmInfo(records[0].Flag("IsEnabled_InitialValue") ?? true, version), false);
        }
        catch (ElevationRequiredException)
        {
            return (null, true);
        }
        catch (CimQueryException)
        {
            return (null, false);
        }
    }

    /// <inheritdoc />
    public async Task<HardwareSnapshot> GetSnapshotAsync(CancellationToken ct)
    {
        var cpuUsage = native.SampleCpuUsageAsync(TimeSpan.FromMilliseconds(AppConfig.CpuSampleMs), ct);
        var processors = QueryAsync(CimScopes.Cimv2, "SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed, L2CacheSize, L3CacheSize, VirtualizationFirmwareEnabled FROM Win32_Processor", ct);
        var modules = QueryAsync(CimScopes.Cimv2, "SELECT Capacity, SMBIOSMemoryType, ConfiguredClockSpeed, Manufacturer FROM Win32_PhysicalMemory", ct);
        var arrays = QueryAsync(CimScopes.Cimv2, "SELECT MemoryDevices FROM Win32_PhysicalMemoryArray", ct);
        var video = QueryAsync(CimScopes.Cimv2, "SELECT Name, DriverVersion, DriverDate FROM Win32_VideoController", ct);
        var board = QueryAsync(CimScopes.Cimv2, "SELECT Manufacturer, Product FROM Win32_BaseBoard", ct);
        var bios = QueryAsync(CimScopes.Cimv2, "SELECT Manufacturer, SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS", ct);
        var disks = QueryAsync(CimScopes.Storage, "SELECT DeviceId, FriendlyName, MediaType, BusType, Size, HealthStatus FROM MSFT_PhysicalDisk", ct);
        var partitions = QueryAsync(CimScopes.Storage, "SELECT DiskNumber, DriveLetter FROM MSFT_Partition", ct);
        var reliability = QueryAsync(CimScopes.Storage, "SELECT DeviceId, Temperature FROM MSFT_StorageReliabilityCounter", ct);
        var tpm = ReadTpmAsync(cim, ct);
        var adapters = Task.Run(dxgi.GetAdapters, ct);
        var cache = Task.Run(native.GetL1CacheBytes, ct);
        var displays = Task.Run(native.GetDisplays, ct);

        await Task.WhenAll(cpuUsage, processors, modules, arrays, video, board, bios, disks, partitions, reliability, tpm, adapters, cache, displays).ConfigureAwait(false);

        var (tpmInfo, tpmAdmin) = await tpm.ConfigureAwait(false);
        return new HardwareSnapshot(
            time.GetLocalNow(),
            Cpu(await processors.ConfigureAwait(false), await cache.ConfigureAwait(false), await cpuUsage.ConfigureAwait(false)),
            Memory(await modules.ConfigureAwait(false), await arrays.ConfigureAwait(false)),
            Gpus(await adapters.ConfigureAwait(false), await video.ConfigureAwait(false)),
            await displays.ConfigureAwait(false),
            Board(await board.ConfigureAwait(false), await bios.ConfigureAwait(false), tpmInfo, tpmAdmin),
            Volumes(await disks.ConfigureAwait(false), await partitions.ConfigureAwait(false), await reliability.ConfigureAwait(false)),
            (await disks.ConfigureAwait(false)).Count);
    }

    /// <summary>Merges the DXGI adapters with the CIM driver data, virtual adapters excluded.</summary>
    /// <param name="adapters">DXGI hardware adapters.</param>
    /// <param name="controllers">CIM video controllers.</param>
    /// <returns>The physical graphics adapters.</returns>
    internal static IReadOnlyList<GpuInfo> Gpus(IReadOnlyList<GpuAdapter> adapters, IReadOnlyList<CimRecord> controllers)
    {
        CimRecord? Controller(string name) =>
            controllers.FirstOrDefault(c => string.Equals(c.Text("Name"), name, StringComparison.OrdinalIgnoreCase));

        if (adapters.Count > 0)
        {
            return [.. adapters
                .Where(a => !IsVirtual(a.Description))
                .Select(a => new GpuInfo(a.Description, a.DedicatedMemoryBytes, Controller(a.Description)?.Text("DriverVersion"), Controller(a.Description)?.Date("DriverDate"), a.FeatureLevel))];
        }

        return [.. controllers
            .Where(c => c.Text("Name") is { } name && !IsVirtual(name))
            .Select(c => new GpuInfo(c.Text("Name")!, null, c.Text("DriverVersion"), c.Date("DriverDate"), null))];
    }

    /// <summary>Builds the volumes of the fixed disks, and an entry for a disk without a mounted volume.</summary>
    /// <param name="disks">Physical disks.</param>
    /// <param name="partitions">Partitions and their drive letter.</param>
    /// <param name="reliability">Reliability counters (temperature).</param>
    /// <returns>The volumes, by disk number then letter.</returns>
    internal static IReadOnlyList<VolumeInfo> Volumes(IReadOnlyList<CimRecord> disks, IReadOnlyList<CimRecord> partitions, IReadOnlyList<CimRecord> reliability)
    {
        var letterToDisk = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var partition in partitions)
        {
            if (partition.Number("DriveLetter") is { } code and > 0 && partition.Number("DiskNumber") is { } disk)
            {
                letterToDisk[$"{(char)code}:"] = (int)disk;
            }
        }

        var systemLetter = Path.GetPathRoot(Environment.SystemDirectory)?.TrimEnd('\\');
        var volumes = new List<VolumeInfo>();
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
        {
            var letter = drive.Name.TrimEnd('\\');
            var diskNumber = letterToDisk.TryGetValue(letter, out var n) ? n : -1;
            var disk = disks.FirstOrDefault(d => d.Text("DeviceId") == diskNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));
            volumes.Add(Volume(disk, diskNumber, letter, drive.DriveFormat, drive.TotalSize, drive.TotalSize - drive.TotalFreeSpace, string.Equals(letter, systemLetter, StringComparison.OrdinalIgnoreCase), reliability));
        }

        foreach (var disk in disks)
        {
            if (int.TryParse(disk.Text("DeviceId"), out var number) && !volumes.Exists(v => v.DiskNumber == number))
            {
                volumes.Add(Volume(disk, number, null, null, disk.Number("Size") ?? 0, 0, false, reliability));
            }
        }

        return [.. volumes.OrderBy(v => v.DiskNumber < 0 ? int.MaxValue : v.DiskNumber).ThenBy(v => v.DriveLetter, StringComparer.OrdinalIgnoreCase)];
    }

    // Firmware often reports a placeholder instead of the module manufacturer.
    private static string? KnownText(string? text) => text is null || text.Equals("Unknown", StringComparison.OrdinalIgnoreCase) ? null : text;

    private static bool IsVirtual(string name) => VirtualAdapterMarkers.Any(m => name.Contains(m, StringComparison.OrdinalIgnoreCase));

    private static VolumeInfo Volume(CimRecord? disk, int number, string? letter, string? fileSystem, long capacity, long used, bool isSystem, IReadOnlyList<CimRecord> reliability)
    {
        var temperature = reliability.FirstOrDefault(r => r.Text("DeviceId") == disk?.Text("DeviceId"))?.Number("Temperature");
        return new VolumeInfo(
            number,
            letter,
            disk?.Text("FriendlyName") ?? string.Empty,
            Media(disk?.Number("MediaType"), disk?.Number("BusType")),
            fileSystem,
            capacity,
            used,
            isSystem,
            disk?.Number("HealthStatus") switch
            {
                0 => DiskHealth.Good,
                1 => DiskHealth.Warning,
                2 => DiskHealth.Unhealthy,
                _ => DiskHealth.Unknown,
            },
            temperature is > 0 ? (int)temperature : null);
    }

    private static MediaKind Media(long? media, long? bus) => (media, bus) switch
    {
        (_, BusNvme) => MediaKind.SsdNvme,
        (MediaSsd, BusSata) => MediaKind.SsdSata,
        (MediaHdd, BusUsb) => MediaKind.HddUsb,
        (MediaHdd, _) => MediaKind.Hdd,
        _ => MediaKind.Other,
    };

    private static CpuInfo Cpu(IReadOnlyList<CimRecord> processors, long? l1, double? usage)
    {
        long? Sum(string name) => processors.Count == 0 ? null : processors.Sum(p => p.Number(name) ?? 0);
        return new CpuInfo(
            CimRecord.FirstOf(processors)?.Text("Name"),
            (int?)Sum("NumberOfCores"),
            (int?)Sum("NumberOfLogicalProcessors"),
            CimRecord.FirstOf(processors)?.Number("MaxClockSpeed"),
            processors.Count == 0 ? null : processors.Count,
            l1,
            Sum("L2CacheSize") * KiB,
            Sum("L3CacheSize") * KiB,
            CimRecord.FirstOf(processors)?.Flag("VirtualizationFirmwareEnabled"),
            usage);
    }

    private static string? MemoryType(long? smbiosType) => smbiosType switch
    {
        20 => "DDR",
        21 => "DDR2",
        24 => "DDR3",
        26 => "DDR4",
        27 => "LPDDR",
        28 => "LPDDR2",
        29 => "LPDDR3",
        30 => "LPDDR4",
        34 => "DDR5",
        35 => "LPDDR5",
        _ => null,
    };

    private BoardInfo Board(IReadOnlyList<CimRecord> board, IReadOnlyList<CimRecord> bios, TpmInfo? tpm, bool tpmAdmin) => new(
        CimRecord.FirstOf(board)?.Text("Manufacturer"),
        CimRecord.FirstOf(board)?.Text("Product"),
        CimRecord.FirstOf(bios)?.Text("Manufacturer"),
        CimRecord.FirstOf(bios)?.Text("SMBIOSBIOSVersion"),
        CimRecord.FirstOf(bios)?.Date("ReleaseDate"),
        native.GetFirmwareType(),
        tpm,
        tpmAdmin);

    private MemoryInfo Memory(IReadOnlyList<CimRecord> modules, IReadOnlyList<CimRecord> arrays)
    {
        var status = native.GetMemoryStatus();
        return new MemoryInfo(
            status?.TotalBytes ?? 0,
            status?.AvailableBytes ?? 0,
            MemoryType(CimRecord.FirstOf(modules)?.Number("SMBIOSMemoryType")),
            (int?)CimRecord.FirstOf(modules)?.Number("ConfiguredClockSpeed"),
            [.. modules.Select(m => new MemoryModule(m.Number("Capacity") ?? 0, KnownText(m.Text("Manufacturer"))))],
            arrays.Count == 0 ? null : (int)arrays.Sum(a => a.Number("MemoryDevices") ?? 0));
    }

    private async Task<IReadOnlyList<CimRecord>> QueryAsync(string scope, string wql, CancellationToken ct)
    {
        try
        {
            return await cim.QueryAsync(scope, wql, ct).ConfigureAwait(false);
        }
        catch (ElevationRequiredException)
        {
            // Expected in standard rights (disk temperature): the value shows "not available".
            LogNeedsAdmin(logger, wql);
            return [];
        }
        catch (CimQueryException ex)
        {
            // Partial analysis (F-LIM-07): the value shows "not available", the cause goes to the log.
            LogQueryFailed(logger, wql, ex);
            return [];
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Requete CIM en echec : {Query}")]
    private static partial void LogQueryFailed(ILogger logger, string query, Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Requete CIM reservee aux administrateurs : {Query}")]
    private static partial void LogNeedsAdmin(ILogger logger, string query);
}
