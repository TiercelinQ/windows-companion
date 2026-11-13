namespace WindowsCompanion.Models;

/// <summary>Outcome of a check for a newer version of the application.</summary>
public enum UpdateCheckStatus
{
    /// <summary>No request was made: the check is turned off or ran less than a day ago.</summary>
    Skipped,

    /// <summary>The installed version is the latest release.</summary>
    UpToDate,

    /// <summary>A release newer than the installed version exists.</summary>
    Available,
}

/// <summary>Result of a check for a newer version of the application (D-13).</summary>
/// <param name="Status">What the check found.</param>
/// <param name="LatestVersion">Latest release version, for example <c>1.1.0</c>; null when <see cref="UpdateCheckStatus.Skipped"/>.</param>
public sealed record UpdateCheckResult(UpdateCheckStatus Status, string? LatestVersion);
