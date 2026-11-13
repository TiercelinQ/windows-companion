namespace WindowsCompanion.Models;

/// <summary>Theme chosen by the user.</summary>
public enum ThemeMode
{
    /// <summary>Follows the Windows app mode.</summary>
    System,

    /// <summary>Always light.</summary>
    Light,

    /// <summary>Always dark.</summary>
    Dark,
}

/// <summary>Saved bounds of the main window.</summary>
/// <param name="Left">Left edge, in DIP.</param>
/// <param name="Top">Top edge, in DIP.</param>
/// <param name="Width">Width, in DIP.</param>
/// <param name="Height">Height, in DIP.</param>
/// <param name="Maximized">Whether the window was maximized.</param>
public sealed record WindowPlacement(double Left, double Top, double Width, double Height, bool Maximized);

/// <summary>An application the user asked never to be offered again.</summary>
/// <param name="Id">Package identifier.</param>
/// <param name="Name">Display name.</param>
/// <param name="Reason">Reason shown in Settings.</param>
/// <param name="AddedAt">When the application was ignored.</param>
public sealed record ExclusionEntry(string Id, string Name, string Reason, DateTimeOffset AddedAt);

/// <summary>User settings persisted in <c>preferences.json</c> (cahier 5.4, v1.0 scope).</summary>
/// <remarks>Setters, not init: the JSON source generator assigns every init-only property, so a key missing from the file would erase its default.</remarks>
public sealed record AppSettings
{
    /// <summary>Gets the file format version.</summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Gets a value indicating whether the winget source is searched.</summary>
    public bool UseWinget { get; set; } = true;

    /// <summary>Gets a value indicating whether the Microsoft Store source is searched.</summary>
    public bool UseMsStore { get; set; } = true;

    /// <summary>Gets a value indicating whether applications with an unknown version are listed.</summary>
    public bool IncludeUnknownVersions { get; set; }

    /// <summary>Gets a value indicating whether installers run without a window.</summary>
    public bool SilentInstall { get; set; } = true;

    /// <summary>Gets the theme mode.</summary>
    public ThemeMode Theme { get; set; } = ThemeMode.System;

    /// <summary>Gets a value indicating whether the navigation pane is folded to icons.</summary>
    public bool NavigationCollapsed { get; set; }

    /// <summary>Gets the saved window bounds, or null on first launch.</summary>
    public WindowPlacement? Window { get; set; }

    /// <summary>Gets a value indicating whether the updates are searched when the application starts (off by default).</summary>
    public bool AutoSearchUpdates { get; set; }

    /// <summary>Gets the time of the last update search, or null before the first one.</summary>
    public DateTimeOffset? LastSearchAt { get; set; }

    /// <summary>Gets a value indicating whether a newer version of the application is looked for at startup (D-13).</summary>
    public bool CheckAppUpdates { get; set; } = true;

    /// <summary>Gets the time of the last successful check for a newer version, or null before the first one.</summary>
    public DateTimeOffset? LastAppUpdateCheckAt { get; set; }

    /// <summary>Gets the ignored applications.</summary>
    public IReadOnlyList<ExclusionEntry> Exclusions { get; set; } = [];
}
