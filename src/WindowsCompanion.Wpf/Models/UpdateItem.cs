namespace WindowsCompanion.Models;

/// <summary>Source of an update.</summary>
public enum UpdateSource
{
    /// <summary>The winget community repository.</summary>
    Winget,

    /// <summary>The Microsoft Store (msstore source).</summary>
    MsStore,
}

/// <summary>Life cycle of an update item (cahier 2.5).</summary>
public enum UpdateState
{
    /// <summary>Found by the last search.</summary>
    Available,

    /// <summary>Waiting in the installation queue.</summary>
    Queued,

    /// <summary>Downloading.</summary>
    Downloading,

    /// <summary>Installer running.</summary>
    Installing,

    /// <summary>Installed.</summary>
    Installed,

    /// <summary>Installed, but the application is open and must be closed.</summary>
    AppOpen,

    /// <summary>Installation failed.</summary>
    Failed,

    /// <summary>Removed from the queue or interrupted.</summary>
    Cancelled,
}

/// <summary>An application for which a newer version is available.</summary>
/// <param name="Id">Package identifier, unique in the list.</param>
/// <param name="Name">Display name.</param>
/// <param name="InstalledVersion">Installed version, null when unknown.</param>
/// <param name="AvailableVersion">Available version.</param>
/// <param name="Source">Source that offers the update.</param>
/// <param name="SizeBytes">Download size, null when winget does not report it (R7).</param>
public sealed record UpdateItem(
    string Id,
    string Name,
    string? InstalledVersion,
    string AvailableVersion,
    UpdateSource Source,
    long? SizeBytes);

/// <summary>Result of an update search: the items, and the sources that did not answer (F-LIM-03).</summary>
/// <param name="Items">Updates found, ignored applications excluded by the caller.</param>
/// <param name="FailedSources">Sources whose search failed.</param>
public sealed record UpgradeSearchResult(IReadOnlyList<UpdateItem> Items, IReadOnlyList<UpdateSource> FailedSources);

/// <summary>Figures shared by the overview tiles and recommended actions.</summary>
/// <param name="HasSearched">Whether a search has run in this session.</param>
/// <param name="PendingCount">Updates neither installed nor waiting for an application to close.</param>
/// <param name="KnownBytes">Sum of the known download sizes.</param>
/// <param name="HasUnknownSize">Whether at least one pending size is unknown.</param>
/// <param name="LargestName">Name of the largest known download, if any.</param>
/// <param name="LargestBytes">Size of the largest known download.</param>
public sealed record UpdatesSummary(
    bool HasSearched,
    int PendingCount,
    long KnownBytes,
    bool HasUnknownSize,
    string? LargestName,
    long LargestBytes)
{
    /// <summary>Gets the summary before any search.</summary>
    public static UpdatesSummary NotSearched { get; } = new(false, 0, 0, false, null, 0);
}
