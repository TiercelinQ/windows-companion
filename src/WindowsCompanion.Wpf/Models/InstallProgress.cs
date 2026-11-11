namespace WindowsCompanion.Models;

/// <summary>Phase of an installation in progress.</summary>
public enum InstallPhase
{
    /// <summary>The package is being downloaded.</summary>
    Downloading,

    /// <summary>The installer is running.</summary>
    Installing,
}

/// <summary>Progress reported while an item is processed.</summary>
/// <param name="Phase">Current phase.</param>
/// <param name="Percent">Download progress, 0 to 100; ignored during installation.</param>
public sealed record InstallProgress(InstallPhase Phase, double Percent);
