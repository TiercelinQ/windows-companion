using WindowsCompanion.Models;

namespace WindowsCompanion.ViewModels;

/// <summary>The update list changed (search, installation, ignore).</summary>
/// <param name="Summary">Current figures.</param>
/// <param name="Pending">Updates not yet installed, for the full report.</param>
public sealed record UpdatesChangedMessage(UpdatesSummary Summary, IReadOnlyList<UpdateItem> Pending);

/// <summary>A hardware analysis ended.</summary>
/// <param name="Snapshot">The snapshot.</param>
public sealed record HardwareSnapshotMessage(HardwareSnapshot Snapshot);

/// <summary>A system reading ended.</summary>
/// <param name="Snapshot">The snapshot.</param>
public sealed record SystemSnapshotMessage(SystemSnapshot Snapshot);

/// <summary>The adapters were read, and possibly tested.</summary>
/// <param name="Snapshot">The adapters.</param>
/// <param name="InternetElapsedMs">Duration of the last Internet test, when it succeeded.</param>
public sealed record NetworkSnapshotMessage(NetworkSnapshot Snapshot, long? InternetElapsedMs);

/// <summary>A search of the updates is requested from another page or a toast.</summary>
public sealed record SearchUpdatesRequestedMessage;

/// <summary>A relaunch as administrator is requested from a toast action.</summary>
/// <param name="Page">Page to reopen.</param>
public sealed record ElevationRequestedMessage(PageKey Page);

/// <summary>The settings changed: sources, options or ignored applications (A7).</summary>
public sealed record SettingsChangedMessage;

/// <summary>The user asked, from Settings, to look for a newer version of the application (D-13).</summary>
public sealed record AppUpdateCheckRequestedMessage;

/// <summary>An entry was added to the history.</summary>
public sealed record HistoryChangedMessage;
