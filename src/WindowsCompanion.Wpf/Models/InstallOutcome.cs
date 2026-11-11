namespace WindowsCompanion.Models;

/// <summary>How an installation ended.</summary>
public enum InstallResultKind
{
    /// <summary>winget reported success.</summary>
    Installed,

    /// <summary>winget or the installer reported an error.</summary>
    Failed,

    /// <summary>The user cancelled it.</summary>
    Cancelled,
}

/// <summary>Result of one installation.</summary>
/// <param name="Kind">How it ended.</param>
/// <param name="ExitCode">Exit code formatted for display (decimal, or 0x8A15xxxx for winget), set on failure.</param>
public sealed record InstallOutcome(InstallResultKind Kind, string? ExitCode = null);
