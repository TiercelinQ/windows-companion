using System.ComponentModel;
using Microsoft.Extensions.Logging;
using WindowsCompanion.Helpers;
using WindowsCompanion.Models;
using WindowsCompanion.Resources;
using WindowsCompanion.Services.Winget;

namespace WindowsCompanion.Services;

/// <summary>Searches, installs and cancels updates through the winget command line (D2).</summary>
public interface IWingetService
{
    /// <summary>Reads the winget version.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The version, or a failure when winget is missing (F-LIM-01).</returns>
    Task<Result<string>> GetVersionAsync(CancellationToken ct);

    /// <summary>Searches the updates, one call per source; a failed source does not stop the other (F-LIM-03).</summary>
    /// <param name="sources">Active sources.</param>
    /// <param name="includeUnknown">Whether to list applications with an unknown version.</param>
    /// <param name="log">Receives the log lines, possibly on a background thread.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The items and the failed sources; a failure when no source answered (N-22).</returns>
    Task<Result<UpgradeSearchResult>> SearchUpgradesAsync(IReadOnlyList<UpdateSource> sources, bool includeUnknown, Action<WingetLogLine> log, CancellationToken ct);

    /// <summary>Installs the update of one package.</summary>
    /// <param name="item">Item to install.</param>
    /// <param name="silent">Whether the installer runs without a window.</param>
    /// <param name="progress">Receives the phase and the download progress.</param>
    /// <param name="log">Receives the log lines, possibly on a background thread.</param>
    /// <param name="ct">Cancellation token; cancelling stops winget and the installer.</param>
    /// <returns>How the installation ended.</returns>
    Task<InstallOutcome> InstallAsync(UpdateItem item, bool silent, IProgress<InstallProgress> progress, Action<WingetLogLine> log, CancellationToken ct);
}

/// <summary>Command-line implementation of <see cref="IWingetService"/>.</summary>
/// <param name="runner">Runs winget.</param>
/// <param name="logger">Logger.</param>
public sealed partial class WingetService(IProcessRunner runner, ILogger<WingetService> logger) : IWingetService
{
    /// <inheritdoc />
    public async Task<Result<string>> GetVersionAsync(CancellationToken ct)
    {
        try
        {
            var result = await runner.RunAsync(AppConfig.WingetExecutable, WingetArguments.Version, null, ct).ConfigureAwait(false);
            var version = WingetOutputParser.ParseVersion(result.StandardOutput);
            return result.ExitCode == 0 && version is not null
                ? Result<string>.Success(version)
                : Result<string>.Failure(new(FeedbackType.Danger, Strings.Winget_NotFound));
        }
        catch (Win32Exception ex)
        {
            LogNotFound(logger, ex);
            return Result<string>.Failure(new(FeedbackType.Danger, Strings.Winget_NotFound));
        }
    }

    /// <inheritdoc />
    public async Task<Result<UpgradeSearchResult>> SearchUpgradesAsync(
        IReadOnlyList<UpdateSource> sources,
        bool includeUnknown,
        Action<WingetLogLine> log,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(log);
        var items = new List<UpdateItem>();
        var failed = new List<UpdateSource>();
        string? cause = null;

        foreach (var source in sources)
        {
            var args = WingetArguments.Upgrade(source, includeUnknown);
            log(new WingetLogLine(LogLineKind.Command, WingetArguments.Display(args)));
            try
            {
                var result = await runner.RunAsync(AppConfig.WingetExecutable, args, null, ct).ConfigureAwait(false);
                var found = WingetOutputParser.ParseUpgradeTable(result.StandardOutput, source);
                if (found.Count == 0 && !WingetOutputParser.IsEmptyResult(result.ExitCode))
                {
                    failed.Add(source);
                    cause = Formats.Template(Strings.Winget_ExitCodeCause, WingetOutputParser.FormatExitCode(result.ExitCode));
                    log(new WingetLogLine(LogLineKind.Error, Formats.Template(Strings.Log_SourceFailed, WingetArguments.SourceName(source), cause)));
                    continue;
                }

                items.AddRange(found.Where(f => !items.Exists(i => string.Equals(i.Id, f.Id, StringComparison.OrdinalIgnoreCase))));
                log(new WingetLogLine(LogLineKind.Success, Formats.Template(Strings.Log_SourceUpdated, WingetArguments.SourceName(source))));
            }
            catch (Win32Exception ex)
            {
                LogNotFound(logger, ex);
                log(new WingetLogLine(LogLineKind.Error, Strings.Winget_NotFound));
                return Result<UpgradeSearchResult>.Failure(new(
                    FeedbackType.Danger,
                    Strings.Toast_SearchFailed_Title,
                    Formats.Template(Strings.Toast_SearchFailed_Message, Strings.Winget_NotFoundCause)));
            }
        }

        if (sources.Count > 0 && failed.Count == sources.Count)
        {
            return Result<UpgradeSearchResult>.Failure(new(
                FeedbackType.Danger,
                Strings.Toast_SearchFailed_Title,
                Formats.Template(Strings.Toast_SearchFailed_Message, cause ?? Strings.Winget_NotFoundCause)));
        }

        log(new WingetLogLine(LogLineKind.Success, Formats.Plural(items.Count, Strings.Log_FoundOne, Strings.Log_FoundMany)));
        return Result<UpgradeSearchResult>.Success(new UpgradeSearchResult(items, failed));
    }

    /// <inheritdoc />
    public async Task<InstallOutcome> InstallAsync(
        UpdateItem item,
        bool silent,
        IProgress<InstallProgress> progress,
        Action<WingetLogLine> log,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(log);

        var args = WingetArguments.Install(item, silent);
        log(new WingetLogLine(LogLineKind.Command, WingetArguments.Display(args)));
        log(new WingetLogLine(LogLineKind.Other, Formats.Template(Strings.Log_Downloading, item.Name, item.AvailableVersion)));
        progress.Report(new InstallProgress(InstallPhase.Downloading, 0));

        var installing = 0;
        void OnLine(string raw)
        {
            var line = WingetOutputParser.Clean(raw);
            if (WingetOutputParser.ParseDownloadPercent(line) is { } percent)
            {
                if (Volatile.Read(ref installing) == 0)
                {
                    progress.Report(new InstallProgress(InstallPhase.Downloading, percent));
                    if (percent >= 100 && Interlocked.Exchange(ref installing, 1) == 0)
                    {
                        progress.Report(new InstallProgress(InstallPhase.Installing, 100));
                        log(new WingetLogLine(LogLineKind.Other, Strings.Log_Installing));
                    }
                }

                return;
            }

            if (!WingetOutputParser.IsNoise(line))
            {
                log(new WingetLogLine(LogLineKind.Other, line));
            }
        }

        try
        {
            var result = await runner.RunAsync(AppConfig.WingetExecutable, args, OnLine, ct).ConfigureAwait(false);
            if (result.ExitCode == 0)
            {
                log(new WingetLogLine(LogLineKind.Success, Strings.Log_Installed));
                return new InstallOutcome(InstallResultKind.Installed);
            }

            var code = WingetOutputParser.FormatExitCode(result.ExitCode);
            log(new WingetLogLine(LogLineKind.Error, Formats.Template(Strings.Log_InstallFailed, code)));
            LogInstallFailed(logger, item.Id, code);
            return new InstallOutcome(InstallResultKind.Failed, code);
        }
        catch (OperationCanceledException)
        {
            log(new WingetLogLine(LogLineKind.Error, Formats.Template(Strings.Log_Cancelled, item.Name)));
            return new InstallOutcome(InstallResultKind.Cancelled);
        }
        catch (Win32Exception ex)
        {
            LogNotFound(logger, ex);
            log(new WingetLogLine(LogLineKind.Error, Strings.Winget_NotFound));
            return new InstallOutcome(InstallResultKind.Failed, Strings.Winget_NotFoundCode);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "winget introuvable")]
    private static partial void LogNotFound(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Echec de l'installation de {PackageId}, code {ExitCode}")]
    private static partial void LogInstallFailed(ILogger logger, string packageId, string exitCode);
}
