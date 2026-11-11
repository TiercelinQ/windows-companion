using System.Runtime.InteropServices;
using WindowsCompanion.Models;

namespace WindowsCompanion.Services.Platform;

/// <summary>Physical memory visible to Windows and available.</summary>
/// <param name="TotalBytes">Visible memory.</param>
/// <param name="AvailableBytes">Available memory.</param>
public sealed record MemoryStatus(long TotalBytes, long AvailableBytes);

/// <summary>Values read through the Windows API.</summary>
public interface INativeSystemInfo
{
    /// <summary>Reads the memory status.</summary>
    /// <returns>The status, or null on failure.</returns>
    MemoryStatus? GetMemoryStatus();

    /// <summary>Samples the processor usage over a window.</summary>
    /// <param name="window">Sampling duration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A percentage, or null on failure.</returns>
    Task<double?> SampleCpuUsageAsync(TimeSpan window, CancellationToken ct);

    /// <summary>Reads the boot firmware type.</summary>
    /// <returns>UEFI or BIOS, or null when unknown.</returns>
    FirmwareKind? GetFirmwareType();

    /// <summary>Reads the total L1 cache size (data and instructions, all cores).</summary>
    /// <returns>Bytes, or null on failure.</returns>
    long? GetL1CacheBytes();

    /// <summary>Lists the connected displays and their resolution.</summary>
    /// <returns>The displays.</returns>
    IReadOnlyList<DisplayInfo> GetDisplays();
}

/// <summary>Windows API implementation of <see cref="INativeSystemInfo"/>.</summary>
public sealed class NativeSystemInfo : INativeSystemInfo
{
    private const int FirmwareBios = 1;
    private const int FirmwareUefi = 2;
    private const int CacheLevelOffset = 8;
    private const int CacheSizeOffset = 12;

    /// <inheritdoc />
    public MemoryStatus? GetMemoryStatus()
    {
        var status = new NativeMethods.MemoryStatusEx { Length = (uint)Marshal.SizeOf<NativeMethods.MemoryStatusEx>() };
        return NativeMethods.GlobalMemoryStatusEx(ref status)
            ? new MemoryStatus((long)status.TotalPhys, (long)status.AvailPhys)
            : null;
    }

    /// <inheritdoc />
    public async Task<double?> SampleCpuUsageAsync(TimeSpan window, CancellationToken ct)
    {
        if (!NativeMethods.GetSystemTimes(out var idle0, out var kernel0, out var user0))
        {
            return null;
        }

        await Task.Delay(window, ct).ConfigureAwait(false);
        if (!NativeMethods.GetSystemTimes(out var idle1, out var kernel1, out var user1))
        {
            return null;
        }

        // Kernel time includes idle time.
        var total = (kernel1 - kernel0) + (user1 - user0);
        return total <= 0 ? null : Math.Clamp((1 - ((double)(idle1 - idle0) / total)) * 100, 0, 100);
    }

    /// <inheritdoc />
    public FirmwareKind? GetFirmwareType() =>
        NativeMethods.GetFirmwareType(out var type)
            ? type switch
            {
                FirmwareUefi => FirmwareKind.Uefi,
                FirmwareBios => FirmwareKind.Bios,
                _ => null,
            }
            : null;

    /// <inheritdoc />
    public unsafe long? GetL1CacheBytes()
    {
        uint length = 0;
        NativeMethods.GetLogicalProcessorInformationEx(NativeMethods.RelationCache, null, ref length);
        if (length == 0)
        {
            return null;
        }

        var buffer = new byte[length];
        fixed (byte* start = buffer)
        {
            if (!NativeMethods.GetLogicalProcessorInformationEx(NativeMethods.RelationCache, start, ref length))
            {
                return null;
            }

            // SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX: Relationship (4), Size (4), then CACHE_RELATIONSHIP:
            // Level (1), Associativity (1), LineSize (2), CacheSize (4).
            long total = 0;
            uint offset = 0;
            while (offset < length)
            {
                var entry = start + offset;
                var size = *(uint*)(entry + 4);
                if (*(int*)entry == NativeMethods.RelationCache && entry[CacheLevelOffset] == 1)
                {
                    total += *(uint*)(entry + CacheSizeOffset);
                }

                if (size == 0)
                {
                    break;
                }

                offset += size;
            }

            return total > 0 ? total : null;
        }
    }

    /// <inheritdoc />
    public unsafe IReadOnlyList<DisplayInfo> GetDisplays()
    {
        var displays = new List<DisplayInfo>();
        var handle = GCHandle.Alloc(displays);
        try
        {
            NativeMethods.EnumDisplayMonitors(0, 0, &OnMonitor, GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }

        return displays;
    }

    // Monitor rectangles are in physical pixels: the process is per-monitor DPI aware (app.manifest).
    [UnmanagedCallersOnly]
    private static unsafe int OnMonitor(nint monitor, nint hdc, NativeMethods.Rect* bounds, nint data)
    {
        if (GCHandle.FromIntPtr(data).Target is List<DisplayInfo> displays)
        {
            displays.Add(new DisplayInfo(bounds->Right - bounds->Left, bounds->Bottom - bounds->Top));
        }

        return 1;
    }
}
