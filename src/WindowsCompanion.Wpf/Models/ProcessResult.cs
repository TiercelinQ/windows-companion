namespace WindowsCompanion.Models;

/// <summary>Result of an external process run to completion.</summary>
/// <param name="ExitCode">Exit code.</param>
/// <param name="StandardOutput">Standard output.</param>
/// <param name="StandardError">Error output.</param>
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>A running process, as seen by <c>IProcessRunner.GetProcesses</c>.</summary>
/// <param name="Id">Process identifier.</param>
/// <param name="Path">Full path of the executable, null when it cannot be read (higher integrity level).</param>
public sealed record RunningProcess(int Id, string? Path);
