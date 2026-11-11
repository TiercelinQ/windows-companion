namespace WindowsCompanion.Models;

/// <summary>Kind of a line of the winget log, which drives its color (F-MAJ-41).</summary>
public enum LogLineKind
{
    /// <summary>Command sent to winget.</summary>
    Command,

    /// <summary>Successful step.</summary>
    Success,

    /// <summary>Error.</summary>
    Error,

    /// <summary>Any other winget output.</summary>
    Other,
}

/// <summary>A line of the winget log.</summary>
/// <param name="Kind">Kind of line.</param>
/// <param name="Text">Text, cleaned of console control sequences.</param>
public sealed record WingetLogLine(LogLineKind Kind, string Text);
