using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using WindowsCompanion.Models;

namespace WindowsCompanion.Services;

/// <summary>Runs external programs (winget, ipconfig, dotnet, explorer) and inspects running processes (rules/security.md §4).</summary>
public interface IProcessRunner
{
    /// <summary>Runs a program to completion: no shell, arguments as separate elements, both streams read with the wait.</summary>
    /// <param name="fileName">Executable name or full path.</param>
    /// <param name="arguments">Arguments, one element each.</param>
    /// <param name="onOutputLine">Called for each line of either stream, on a background thread.</param>
    /// <param name="ct">Cancellation token; cancelling kills the whole process tree.</param>
    /// <returns>Exit code and outputs.</returns>
    /// <exception cref="Win32Exception">The executable cannot be started (not found).</exception>
    Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, Action<string>? onOutputLine, CancellationToken ct);

    /// <summary>Starts a program without waiting for it.</summary>
    /// <param name="fileName">Full path of the executable.</param>
    /// <param name="arguments">Arguments, one element each.</param>
    void Start(string fileName, IReadOnlyList<string> arguments);

    /// <summary>Lists the running processes and their executable path.</summary>
    /// <returns>The processes.</returns>
    IReadOnlyList<RunningProcess> GetProcesses();

    /// <summary>Asks a process to close its main window, without forcing it.</summary>
    /// <param name="processId">Process identifier.</param>
    /// <param name="timeout">How long to wait for the exit.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when the process has exited.</returns>
    Task<bool> CloseAsync(int processId, TimeSpan timeout, CancellationToken ct);
}

/// <summary><see cref="Process"/> implementation of <see cref="IProcessRunner"/>.</summary>
public sealed class ProcessRunner : IProcessRunner
{
    /// <inheritdoc />
    public async Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, Action<string>? onOutputLine, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var start = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false, // command-injection guard: no shell interprets the arguments
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        var output = new StringBuilder();
        var error = new StringBuilder();
        using var process = new Process { StartInfo = start };
        process.OutputDataReceived += (_, e) => Collect(output, e.Data, onOutputLine);
        process.ErrorDataReceived += (_, e) => Collect(error, e.Data, onOutputLine);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            // Waits for the exit and for both redirected streams (read concurrently, no buffer deadlock).
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            throw;
        }

        lock (output)
        {
            lock (error)
            {
                return new ProcessResult(process.ExitCode, output.ToString(), error.ToString());
            }
        }
    }

    /// <inheritdoc />
    public void Start(string fileName, IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var start = new ProcessStartInfo(fileName) { UseShellExecute = false };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start);
    }

    /// <inheritdoc />
    public IReadOnlyList<RunningProcess> GetProcesses()
    {
        var result = new List<RunningProcess>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                string? path;
                try
                {
                    path = process.MainModule?.FileName;
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    path = null; // higher integrity level or already exited
                }

                result.Add(new RunningProcess(process.Id, path));
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<bool> CloseAsync(int processId, TimeSpan timeout, CancellationToken ct)
    {
        Process process;
        try
        {
            process = Process.GetProcessById(processId);
        }
        catch (ArgumentException)
        {
            return true; // already gone
        }

        using (process)
        {
            if (!process.CloseMainWindow())
            {
                return process.HasExited;
            }

            using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limit.CancelAfter(timeout);
            try
            {
                await process.WaitForExitAsync(limit.Token).ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return false;
            }
        }
    }

    private static void Collect(StringBuilder target, string? line, Action<string>? onOutputLine)
    {
        if (line is null)
        {
            return;
        }

        lock (target)
        {
            target.AppendLine(line);
        }

        onOutputLine?.Invoke(line);
    }

    // Cancellation stops winget and the installer it started (risk R3).
    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            // Already exited.
        }
    }
}
