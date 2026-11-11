namespace WindowsCompanion.Models;

/// <summary>Kind of recommended action (F-ACC-10 to F-ACC-15).</summary>
public enum ActionKind
{
    /// <summary>Updates are pending.</summary>
    Updates,

    /// <summary>No update pending.</summary>
    UpToDate,

    /// <summary>A volume is almost full or full.</summary>
    VolumeFull,

    /// <summary>Windows has been running for a long time.</summary>
    Restart,

    /// <summary>All security items are active.</summary>
    SecurityOk,

    /// <summary>Some security items are inactive.</summary>
    SecurityCheck,
}

/// <summary>Severity of a recommended action, which drives the icon square color.</summary>
public enum ActionSeverity
{
    /// <summary>Information.</summary>
    Info,

    /// <summary>Warning.</summary>
    Warning,

    /// <summary>Success.</summary>
    Success,
}

/// <summary>A recommended action of the overview.</summary>
/// <param name="Kind">Kind.</param>
/// <param name="Severity">Severity.</param>
/// <param name="Title">Title.</param>
/// <param name="Detail">Detail.</param>
/// <param name="ButtonLabel">Label of the button ("Voir" or "Détails").</param>
/// <param name="Target">Page opened by the button.</param>
public sealed record RecommendedAction(
    ActionKind Kind,
    ActionSeverity Severity,
    string Title,
    string Detail,
    string ButtonLabel,
    PageKey Target);
