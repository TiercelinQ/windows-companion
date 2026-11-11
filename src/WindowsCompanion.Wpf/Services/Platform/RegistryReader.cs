using System.IO;
using System.Security;
using Microsoft.Win32;

namespace WindowsCompanion.Services.Platform;

/// <summary>Registry hive read by the application.</summary>
public enum RegistryRoot
{
    /// <summary>HKEY_LOCAL_MACHINE.</summary>
    LocalMachine,

    /// <summary>HKEY_CURRENT_USER.</summary>
    CurrentUser,
}

/// <summary>A registry key location.</summary>
/// <param name="Root">Hive.</param>
/// <param name="Path">Sub-key path.</param>
public sealed record RegistryLocation(RegistryRoot Root, string Path);

/// <summary>Registry paths shared by several services.</summary>
public static class RegistryPaths
{
    /// <summary>Gets the uninstall keys: 64-bit, 32-bit and per-user applications.</summary>
    public static IReadOnlyList<RegistryLocation> Uninstall { get; } =
    [
        new(RegistryRoot.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        new(RegistryRoot.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
        new(RegistryRoot.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
    ];
}

/// <summary>Read-only registry access.</summary>
public interface IRegistryReader
{
    /// <summary>Reads a value.</summary>
    /// <param name="root">Hive.</param>
    /// <param name="subKey">Sub-key path.</param>
    /// <param name="name">Value name.</param>
    /// <returns>The value, or null when the key or the value is missing or unreadable.</returns>
    object? GetValue(RegistryRoot root, string subKey, string name);

    /// <summary>Lists the sub-keys of a key.</summary>
    /// <param name="root">Hive.</param>
    /// <param name="subKey">Sub-key path.</param>
    /// <returns>The names, empty when the key is missing or unreadable.</returns>
    IReadOnlyList<string> GetSubKeyNames(RegistryRoot root, string subKey);
}

/// <summary><see cref="Registry"/> implementation of <see cref="IRegistryReader"/>, 64-bit view.</summary>
public sealed class RegistryReader : IRegistryReader
{
    /// <inheritdoc />
    public object? GetValue(RegistryRoot root, string subKey, string name)
    {
        try
        {
            using var hive = Open(root);
            using var key = hive.OpenSubKey(subKey);
            return key?.GetValue(name);
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetSubKeyNames(RegistryRoot root, string subKey)
    {
        try
        {
            using var hive = Open(root);
            using var key = hive.OpenSubKey(subKey);
            return key?.GetSubKeyNames() ?? [];
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            return [];
        }
    }

    private static RegistryKey Open(RegistryRoot root) =>
        RegistryKey.OpenBaseKey(root == RegistryRoot.LocalMachine ? RegistryHive.LocalMachine : RegistryHive.CurrentUser, RegistryView.Registry64);
}
