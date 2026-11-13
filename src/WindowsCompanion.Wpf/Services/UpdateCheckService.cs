using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using WindowsCompanion.Models;
using WindowsCompanion.Resources;
using WindowsCompanion.Services.Data;

namespace WindowsCompanion.Services;

/// <summary>Looks for a newer release of the application on GitHub (D-13).</summary>
public interface IUpdateCheckService
{
    /// <summary>Compares the installed version with the latest GitHub release.</summary>
    /// <param name="manual">True when the user asked: the setting and the daily limit are then ignored.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The status, or a failure worded for the user when GitHub could not be read.</returns>
    Task<Result<UpdateCheckResult>> CheckAsync(bool manual, CancellationToken ct);

    /// <summary>Opens the releases page in the default browser.</summary>
    void OpenReleasesPage();
}

/// <summary>GitHub implementation of <see cref="IUpdateCheckService"/>.</summary>
/// <param name="http">Shared HTTP client.</param>
/// <param name="preferences">Settings: automatic check and time of the last one.</param>
/// <param name="runner">Starts the Explorer on the releases page.</param>
/// <param name="time">Clock.</param>
/// <param name="logger">Logger.</param>
public sealed partial class UpdateCheckService(
    HttpClient http,
    IPreferencesService preferences,
    IProcessRunner runner,
    TimeProvider time,
    ILogger<UpdateCheckService> logger) : IUpdateCheckService
{
    private static readonly Version CurrentVersion = Normalize(Version.Parse(AppConfig.AppVersion));

    /// <inheritdoc />
    public async Task<Result<UpdateCheckResult>> CheckAsync(bool manual, CancellationToken ct)
    {
        var settings = preferences.Current;
        if (!manual && (!settings.CheckAppUpdates || !IsDue(settings.LastAppUpdateCheckAt)))
        {
            return Result<UpdateCheckResult>.Success(new(UpdateCheckStatus.Skipped, null));
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(AppConfig.UpdateCheckTimeoutMs));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, AppConfig.ReleasesApiUrl);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue(AppConfig.AppName, AppConfig.AppVersion));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using var response = await http.SendAsync(request, timeout.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using var body = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            var release = await JsonSerializer.DeserializeAsync(body, AppJsonContext.Default.GitHubRelease, timeout.Token).ConfigureAwait(false);

            if (!TryParseTag(release?.TagName, out var latest))
            {
                LogTagUnreadable(logger, release?.TagName);
                return Failure();
            }

            // Recorded only for a readable answer: a failure is retried at the next launch.
            var now = time.GetUtcNow();
            await preferences.UpdateAsync(s => s with { LastAppUpdateCheckAt = now }, ct).ConfigureAwait(false);

            var status = latest > CurrentVersion ? UpdateCheckStatus.Available : UpdateCheckStatus.UpToDate;
            return Result<UpdateCheckResult>.Success(new(status, latest.ToString(3)));
        }
        catch (Exception ex) when (!ct.IsCancellationRequested && ex is HttpRequestException or JsonException or OperationCanceledException)
        {
            LogCheckFailed(logger, ex);
            return Failure();
        }
    }

    /// <inheritdoc />
    public void OpenReleasesPage() =>
        runner.Start(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), [AppConfig.ReleasesPageUrl]);

    /// <summary>Reads a release tag such as <c>v1.2.0</c> or <c>1.2.0-beta</c> as a three-part version.</summary>
    /// <param name="tag">The tag.</param>
    /// <param name="version">The version, when the tag is readable.</param>
    /// <returns>True when the tag holds at least major.minor.</returns>
    internal static bool TryParseTag(string? tag, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        var text = tag.Trim().TrimStart('v', 'V');
        var suffix = text.IndexOfAny(['-', '+']);
        if (suffix >= 0)
        {
            text = text[..suffix];
        }

        if (!Version.TryParse(text, out var parsed))
        {
            return false;
        }

        version = Normalize(parsed);
        return true;
    }

    // 1.0.2 and 1.0.2.0 are the same release: the fourth part is dropped and a missing third part is 0.
    private static Version Normalize(Version version) => new(version.Major, version.Minor, Math.Max(version.Build, 0));

    private static Result<UpdateCheckResult> Failure() =>
        Result<UpdateCheckResult>.Failure(new(FeedbackType.Warning, Strings.Toast_CompanionFailed_Title, Strings.Toast_CompanionFailed_Message));

    // A clock set back leaves a last check in the future: the check is then due.
    private bool IsDue(DateTimeOffset? last)
    {
        var now = time.GetUtcNow();
        return last is null || last > now || now - last >= TimeSpan.FromHours(AppConfig.UpdateCheckIntervalHours);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Verification de la version impossible")]
    private static partial void LogCheckFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tag de release illisible : {Tag}")]
    private static partial void LogTagUnreadable(ILogger logger, string? tag);
}
