using System.IO;
using Microsoft.Extensions.Logging;
using WindowsCompanion.Models;
using WindowsCompanion.Services.Data;

namespace WindowsCompanion.Services;

/// <summary>User settings, ignored applications and window state, persisted in <c>preferences.json</c>.</summary>
public interface IPreferencesService
{
    /// <summary>Gets the current settings.</summary>
    AppSettings Current { get; }

    /// <summary>Loads the file once, at startup.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>False when the file was unreadable and the defaults were applied (toast N-30).</returns>
    Task<bool> LoadAsync(CancellationToken ct);

    /// <summary>Changes the settings and writes the file (F-PER-04).</summary>
    /// <param name="change">Builds the new settings from the current ones.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task completed once the file is written.</returns>
    Task UpdateAsync(Func<AppSettings, AppSettings> change, CancellationToken ct);

    /// <summary>Adds ignored applications, without duplicates.</summary>
    /// <param name="entries">Applications to ignore.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task completed once the file is written.</returns>
    Task AddExclusionsAsync(IEnumerable<ExclusionEntry> entries, CancellationToken ct);

    /// <summary>Removes ignored applications.</summary>
    /// <param name="ids">Package identifiers.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task completed once the file is written.</returns>
    Task RemoveExclusionsAsync(IEnumerable<string> ids, CancellationToken ct);
}

/// <summary>JSON implementation of <see cref="IPreferencesService"/>.</summary>
public sealed partial class PreferencesService : IPreferencesService, IDisposable
{
    private readonly IJsonFileStore _store;
    private readonly string _file;
    private readonly ILogger<PreferencesService> _logger;
    private readonly Lock _sync = new();
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private AppSettings _current = new();

    /// <summary>Initializes a new instance of the <see cref="PreferencesService"/> class.</summary>
    /// <param name="store">JSON file access.</param>
    /// <param name="paths">Application locations.</param>
    /// <param name="logger">Logger.</param>
    public PreferencesService(IJsonFileStore store, IAppPaths paths, ILogger<PreferencesService> logger)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _store = store;
        _file = paths.PreferencesFile;
        _logger = logger;
    }

    /// <inheritdoc />
    public AppSettings Current
    {
        get
        {
            lock (_sync)
            {
                return _current;
            }
        }
    }

    /// <inheritdoc />
    public async Task<bool> LoadAsync(CancellationToken ct)
    {
        try
        {
            var loaded = await _store.ReadAsync(_file, AppJsonContext.Default.AppSettings, ct).ConfigureAwait(false);
            lock (_sync)
            {
                // A file written by hand or by another version may hold "exclusions": null.
                _current = loaded is null ? new AppSettings() : loaded with { Exclusions = loaded.Exclusions ?? [] };
            }

            return true;
        }
        catch (StoreCorruptedException)
        {
            return false;
        }
        catch (IOException ex)
        {
            LogReadFailed(_logger, ex);
            return true;
        }
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Func<AppSettings, AppSettings> change, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(change);
        AppSettings snapshot;
        lock (_sync)
        {
            _current = change(_current);
            snapshot = _current;
        }

        await _writeGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _store.WriteAsync(_file, snapshot, AppJsonContext.Default.AppSettings, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogWriteFailed(_logger, ex);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    /// <inheritdoc />
    public Task AddExclusionsAsync(IEnumerable<ExclusionEntry> entries, CancellationToken ct)
    {
        var added = entries.ToList();
        return UpdateAsync(
            settings => settings with
            {
                Exclusions = [.. settings.Exclusions, .. added.Where(e => !settings.Exclusions.Any(x => SameId(x.Id, e.Id))).DistinctBy(e => e.Id, StringComparer.OrdinalIgnoreCase)],
            },
            ct);
    }

    /// <inheritdoc />
    public Task RemoveExclusionsAsync(IEnumerable<string> ids, CancellationToken ct)
    {
        var removed = ids.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return UpdateAsync(settings => settings with { Exclusions = [.. settings.Exclusions.Where(e => !removed.Contains(e.Id))] }, ct);
    }

    /// <inheritdoc />
    public void Dispose() => _writeGate.Dispose();

    private static bool SameId(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    [LoggerMessage(Level = LogLevel.Error, Message = "Echec d'ecriture des preferences")]
    private static partial void LogWriteFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Echec de lecture des preferences, valeurs par defaut")]
    private static partial void LogReadFailed(ILogger logger, Exception exception);
}
