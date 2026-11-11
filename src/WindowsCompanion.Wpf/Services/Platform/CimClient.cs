using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using WindowsCompanion.Models;

namespace WindowsCompanion.Services.Platform;

/// <summary>CIM namespaces read by the application.</summary>
public static class CimScopes
{
    /// <summary>Main namespace.</summary>
    public const string Cimv2 = @"root\CIMV2";

    /// <summary>Storage (physical disks, partitions, reliability counters).</summary>
    public const string Storage = @"root\Microsoft\Windows\Storage";

    /// <summary>Network adapters, firewall, connection profiles.</summary>
    public const string StandardCimv2 = @"root\StandardCimv2";

    /// <summary>Microsoft Defender.</summary>
    public const string Defender = @"root\Microsoft\Windows\Defender";

    /// <summary>TPM (administrator).</summary>
    public const string Tpm = @"root\CIMV2\Security\MicrosoftTpm";

    /// <summary>BitLocker (administrator).</summary>
    public const string BitLocker = @"root\CIMV2\Security\MicrosoftVolumeEncryption";

    /// <summary>System Restore (administrator).</summary>
    public const string Default = @"root\default";
}

/// <summary>An instance returned by a CIM query, as plain values.</summary>
/// <param name="values">Property values by name.</param>
public sealed class CimRecord(IReadOnlyDictionary<string, object?> values)
{
    /// <summary>Gets the first record of a query result.</summary>
    /// <param name="records">Query result, null when the query failed.</param>
    /// <returns>The first record, or null.</returns>
    public static CimRecord? FirstOf(IReadOnlyList<CimRecord>? records) => records is { Count: > 0 } ? records[0] : null;

    /// <summary>Gets a raw property value.</summary>
    /// <param name="name">Property name.</param>
    /// <returns>The value, or null.</returns>
    public object? this[string name] => values.TryGetValue(name, out var value) ? value : null;

    /// <summary>Reads a property as text.</summary>
    /// <param name="name">Property name.</param>
    /// <returns>The trimmed text, null when missing or blank.</returns>
    public string? Text(string name) => this[name] switch
    {
        null => null,
        string s => string.IsNullOrWhiteSpace(s) ? null : s.Trim(),
        var other => Convert.ToString(other, CultureInfo.InvariantCulture),
    };

    /// <summary>Reads a property as an integer.</summary>
    /// <param name="name">Property name.</param>
    /// <returns>The number, or null.</returns>
    public long? Number(string name) => this[name] switch
    {
        null => null,
        string s => long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null,
        bool b => b ? 1 : 0,
        char c => c,
        IConvertible c => c.ToInt64(CultureInfo.InvariantCulture),
        _ => null,
    };

    /// <summary>Reads a property as a boolean.</summary>
    /// <param name="name">Property name.</param>
    /// <returns>The flag, or null.</returns>
    public bool? Flag(string name) => this[name] switch
    {
        null => null,
        bool b => b,
        _ => Number(name) is { } n ? n != 0 : null,
    };

    /// <summary>Reads a CIM date (<c>yyyymmddHHMMSS.mmmmmmsUUU</c>).</summary>
    /// <param name="name">Property name.</param>
    /// <returns>The local date, or null.</returns>
    public DateTime? Date(string name)
    {
        if (Text(name) is not { } text)
        {
            return null;
        }

        try
        {
            return ManagementDateTimeConverter.ToDateTime(text);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}

/// <summary>Read-only CIM/WMI access, and method calls for the two explicit actions.</summary>
public interface ICimClient
{
    /// <summary>Runs a WQL query.</summary>
    /// <param name="scope">Namespace (see <see cref="CimScopes"/>).</param>
    /// <param name="wql">Query, built from constants only.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The instances.</returns>
    /// <exception cref="ElevationRequiredException">The namespace needs administrator rights.</exception>
    /// <exception cref="CimQueryException">Any other failure.</exception>
    Task<IReadOnlyList<CimRecord>> QueryAsync(string scope, string wql, CancellationToken ct);

    /// <summary>Calls a method on the first instance matched by a query.</summary>
    /// <param name="scope">Namespace.</param>
    /// <param name="wql">Query selecting the instance.</param>
    /// <param name="method">Method name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The method return value (0 = success).</returns>
    Task<long> InvokeAsync(string scope, string wql, string method, CancellationToken ct);
}

/// <summary><c>System.Management</c> implementation of <see cref="ICimClient"/>.</summary>
public sealed class CimClient : ICimClient
{
    private const int WbemAccessDenied = unchecked((int)0x80041003);

    /// <inheritdoc />
    public Task<IReadOnlyList<CimRecord>> QueryAsync(string scope, string wql, CancellationToken ct) =>
        Task.Run<IReadOnlyList<CimRecord>>(
            () => Guard(scope, () =>
            {
                using var searcher = Searcher(scope, wql);
                using var results = searcher.Get();
                var records = new List<CimRecord>();
                foreach (var instance in results)
                {
                    using (instance)
                    {
                        ct.ThrowIfCancellationRequested();
                        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                        foreach (var property in instance.Properties)
                        {
                            values[property.Name] = property.Value;
                        }

                        records.Add(new CimRecord(values));
                    }
                }

                return records;
            }),
            ct);

    /// <inheritdoc />
    public Task<long> InvokeAsync(string scope, string wql, string method, CancellationToken ct) =>
        Task.Run(
            () => Guard(scope, () =>
            {
                using var searcher = Searcher(scope, wql);
                using var results = searcher.Get();
                foreach (var instance in results)
                {
                    using (instance)
                    {
                        if (instance is ManagementObject target)
                        {
                            using var output = target.InvokeMethod(method, null, null);
                            return Convert.ToInt64(output?["ReturnValue"] ?? 0L, CultureInfo.InvariantCulture);
                        }
                    }
                }

                throw new CimQueryException($"Aucune instance pour {method}");
            }),
            ct);

    private static ManagementObjectSearcher Searcher(string scope, string wql) => new(
        new ManagementScope(scope),
        new ObjectQuery(wql),
        new EnumerationOptions { Timeout = TimeSpan.FromMilliseconds(AppConfig.CimTimeoutMs), ReturnImmediately = true, Rewindable = false });

    private static T Guard<T>(string scope, Func<T> query)
    {
        try
        {
            return query();
        }
        catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.AccessDenied)
        {
            throw new ElevationRequiredException(scope, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new ElevationRequiredException(scope, ex);
        }
        catch (COMException ex) when (ex.HResult == WbemAccessDenied)
        {
            throw new ElevationRequiredException(scope, ex);
        }
        catch (Exception ex) when (ex is ManagementException or COMException)
        {
            throw new CimQueryException(scope, ex);
        }
    }
}
