using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Security.Principal;
using Microsoft.Extensions.Logging;
using WindowsCompanion.Models;
using WindowsCompanion.Resources;

namespace WindowsCompanion.Services;

/// <summary>Elevation state and relaunch as administrator (cahier 4.11).</summary>
public interface IElevationService
{
    /// <summary>Gets a value indicating whether the process runs with administrator rights.</summary>
    bool IsElevated { get; }

    /// <summary>Starts an elevated instance on the given page; the caller then closes the current one.</summary>
    /// <param name="page">Page to reopen.</param>
    /// <returns>Success once the elevated instance started; a warning when the user declined the UAC prompt (N-29).</returns>
    Result<Unit> RelaunchElevated(PageKey page);
}

/// <summary>Default implementation of <see cref="IElevationService"/>.</summary>
/// <param name="logger">Logger.</param>
public sealed partial class ElevationService(ILogger<ElevationService> logger) : IElevationService
{
    private const int ErrorCancelled = 1223;

    /// <inheritdoc />
    public bool IsElevated { get; } = ReadElevation();

    /// <summary>Builds the arguments of the elevated instance: page to open, process to wait for.</summary>
    /// <param name="page">Page to reopen.</param>
    /// <param name="processId">Current process, which the new instance waits for before taking the single-instance lock.</param>
    /// <returns>The arguments, one element each.</returns>
    public static IReadOnlyList<string> BuildArguments(PageKey page, int processId) =>
    [
        $"--page={PageKeys.ToId(page)}",
        $"--replace={processId.ToString(CultureInfo.InvariantCulture)}",
    ];

    /// <inheritdoc />
    public Result<Unit> RelaunchElevated(PageKey page)
    {
        var path = Environment.ProcessPath;
        if (path is null)
        {
            return Result<Unit>.Failure(new(FeedbackType.Danger, Strings.Elevation_FailedTitle, Strings.Elevation_FailedMessage));
        }

        // D-02: the UAC prompt needs the shell verb "runas". The file is the current executable, never a value
        // from the interface or from data; the arguments stay separate elements.
        var start = new ProcessStartInfo(path) { UseShellExecute = true, Verb = "runas" };
        foreach (var argument in BuildArguments(page, Environment.ProcessId))
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(start);
            return Result<Unit>.Success(Unit.Value);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            LogDeclined(logger);
            return Result<Unit>.Failure(new(FeedbackType.Warning, Strings.Toast_ElevationRefused_Title, Strings.Toast_ElevationRefused_Message));
        }
        catch (Win32Exception ex)
        {
            LogFailed(logger, ex);
            return Result<Unit>.Failure(new(FeedbackType.Danger, Strings.Elevation_FailedTitle, Strings.Elevation_FailedMessage));
        }
    }

    private static bool ReadElevation()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Elevation refusee par l'utilisateur")]
    private static partial void LogDeclined(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Echec de la relance en administrateur")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
