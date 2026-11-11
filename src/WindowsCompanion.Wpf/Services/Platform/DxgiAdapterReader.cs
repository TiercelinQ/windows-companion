using Microsoft.Extensions.Logging;

namespace WindowsCompanion.Services.Platform;

/// <summary>A graphics adapter seen by DXGI.</summary>
/// <param name="Description">Adapter name.</param>
/// <param name="DedicatedMemoryBytes">Dedicated video memory (CIM <c>AdapterRAM</c> is capped at 4 GB, not used).</param>
/// <param name="FeatureLevel">Highest Direct3D 12 feature level, for example <c>12_2</c>.</param>
public sealed record GpuAdapter(string Description, long DedicatedMemoryBytes, string? FeatureLevel);

/// <summary>Reads the hardware graphics adapters through DXGI (software adapters excluded).</summary>
public interface IDxgiAdapterReader
{
    /// <summary>Lists the hardware adapters.</summary>
    /// <returns>The adapters; empty when DXGI is unavailable.</returns>
    IReadOnlyList<GpuAdapter> GetAdapters();
}

/// <summary>DXGI implementation of <see cref="IDxgiAdapterReader"/>, through COM vtable calls (no extra package).</summary>
/// <param name="logger">Logger.</param>
public sealed unsafe partial class DxgiAdapterReader(ILogger<DxgiAdapterReader> logger) : IDxgiAdapterReader
{
    // vtable slots: IUnknown::Release = 2, IDXGIFactory1::EnumAdapters1 = 12, IDXGIAdapter1::GetDesc1 = 10.
    private const int ReleaseSlot = 2;
    private const int EnumAdapters1Slot = 12;
    private const int GetDesc1Slot = 10;
    private const uint SoftwareAdapterFlag = 2;

    private static readonly Guid FactoryId = new("770aae78-f26f-4dba-a829-253c83d1b387");
    private static readonly Guid DeviceId = new("189819f1-1db6-4b57-be54-1821339b85f7");

    // Direct3D feature levels, highest first.
    private static readonly (int Level, string Name)[] FeatureLevels =
    [
        (0xc200, "12_2"),
        (0xc100, "12_1"),
        (0xc000, "12_0"),
        (0xb100, "11_1"),
        (0xb000, "11_0"),
    ];

    /// <inheritdoc />
    public IReadOnlyList<GpuAdapter> GetAdapters()
    {
        var adapters = new List<GpuAdapter>();
        try
        {
            if (NativeMethods.CreateDXGIFactory1(in FactoryId, out var factory) < 0)
            {
                return adapters;
            }

            try
            {
                var enumAdapters = (delegate* unmanaged[Stdcall]<nint, uint, nint*, int>)VTable(factory)[EnumAdapters1Slot];
                for (uint index = 0; ; index++)
                {
                    nint adapter;
                    if (enumAdapters(factory, index, &adapter) < 0)
                    {
                        break; // DXGI_ERROR_NOT_FOUND past the last adapter
                    }

                    try
                    {
                        if (Describe(adapter) is { } described)
                        {
                            adapters.Add(described);
                        }
                    }
                    finally
                    {
                        Release(adapter);
                    }
                }
            }
            finally
            {
                Release(factory);
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            LogUnavailable(logger, ex);
        }

        return adapters;
    }

    private static nint* VTable(nint instance) => *(nint**)instance;

    private static void Release(nint instance) => ((delegate* unmanaged[Stdcall]<nint, uint>)VTable(instance)[ReleaseSlot])(instance);

    private static GpuAdapter? Describe(nint adapter)
    {
        NativeMethods.AdapterDesc1 desc;
        var getDesc = (delegate* unmanaged[Stdcall]<nint, NativeMethods.AdapterDesc1*, int>)VTable(adapter)[GetDesc1Slot];
        if (getDesc(adapter, &desc) < 0 || (desc.Flags & SoftwareAdapterFlag) != 0)
        {
            return null;
        }

        var name = new string(desc.Description).TrimEnd('\0').Trim();
        return new GpuAdapter(name, (long)desc.DedicatedVideoMemory, FeatureLevel(adapter));
    }

    // A null device pointer only tests whether the device could be created (S_FALSE on success).
    private static string? FeatureLevel(nint adapter)
    {
        try
        {
            foreach (var (level, name) in FeatureLevels)
            {
                if (NativeMethods.D3D12CreateDevice(adapter, level, in DeviceId, 0) >= 0)
                {
                    return name;
                }
            }
        }
        catch (DllNotFoundException)
        {
            return null;
        }

        return null;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "DXGI indisponible")]
    private static partial void LogUnavailable(ILogger logger, Exception exception);
}
