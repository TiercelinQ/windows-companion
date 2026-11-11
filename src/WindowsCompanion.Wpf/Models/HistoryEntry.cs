namespace WindowsCompanion.Models;

/// <summary>Result recorded in the history.</summary>
public enum HistoryResult
{
    /// <summary>Installed, including when the application had to be closed.</summary>
    Success,

    /// <summary>Installation failed.</summary>
    Failure,

    /// <summary>Interrupted by the user.</summary>
    Cancelled,
}

/// <summary>An entry of the installation history (cahier 5.3).</summary>
/// <param name="Timestamp">End of the processing.</param>
/// <param name="AppId">Package identifier.</param>
/// <param name="AppName">Display name.</param>
/// <param name="FromVersion">Version before, null when unknown.</param>
/// <param name="ToVersion">Target version.</param>
/// <param name="Source">Source of the update.</param>
/// <param name="Result">Result.</param>
/// <param name="ExitCode">Exit code on failure.</param>
/// <param name="DurationSeconds">Duration, null for a cancellation.</param>
public sealed record HistoryEntry(
    DateTimeOffset Timestamp,
    string AppId,
    string AppName,
    string? FromVersion,
    string ToVersion,
    UpdateSource Source,
    HistoryResult Result,
    string? ExitCode,
    int? DurationSeconds);
