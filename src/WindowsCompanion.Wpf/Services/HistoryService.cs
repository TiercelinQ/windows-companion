using System.IO;
using Microsoft.Extensions.Logging;
using WindowsCompanion.Models;
using WindowsCompanion.Services.Data;

namespace WindowsCompanion.Services;

/// <summary>History entries and whether the file had to be reset.</summary>
/// <param name="Entries">Entries, most recent first.</param>
/// <param name="WasReset">True once, when the file was unreadable and set aside (toast N-30).</param>
public sealed record HistoryLoad(IReadOnlyList<HistoryEntry> Entries, bool WasReset);

/// <summary>Installation history, persisted in <c>history.json</c>.</summary>
public interface IHistoryService
{
    /// <summary>Reads the history.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The entries, most recent first.</returns>
    Task<HistoryLoad> ListAsync(CancellationToken ct);

    /// <summary>Adds an entry at the top, dropping the oldest beyond the limit (F-HIS-10).</summary>
    /// <param name="entry">The entry.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task completed once the file is written.</returns>
    Task AddAsync(HistoryEntry entry, CancellationToken ct);

    /// <summary>Removes every entry.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task completed once the file is written.</returns>
    Task ClearAsync(CancellationToken ct);
}

/// <summary>JSON implementation of <see cref="IHistoryService"/>.</summary>
public sealed partial class HistoryService : IHistoryService, IDisposable
{
    private readonly IJsonFileStore _store;
    private readonly string _file;
    private readonly ILogger<HistoryService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private List<HistoryEntry>? _entries;
    private bool _resetPending;

    /// <summary>Initializes a new instance of the <see cref="HistoryService"/> class.</summary>
    /// <param name="store">JSON file access.</param>
    /// <param name="paths">Application locations.</param>
    /// <param name="logger">Logger.</param>
    public HistoryService(IJsonFileStore store, IAppPaths paths, ILogger<HistoryService> logger)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _store = store;
        _file = paths.HistoryFile;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<HistoryLoad> ListAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var entries = await EnsureLoadedAsync(ct).ConfigureAwait(false);
            var load = new HistoryLoad([.. entries], _resetPending);
            _resetPending = false;
            return load;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task AddAsync(HistoryEntry entry, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var entries = await EnsureLoadedAsync(ct).ConfigureAwait(false);
            entries.Insert(0, entry);
            if (entries.Count > AppConfig.HistoryMaxEntries)
            {
                entries.RemoveRange(AppConfig.HistoryMaxEntries, entries.Count - AppConfig.HistoryMaxEntries);
            }

            await WriteAsync(entries, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task ClearAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var entries = await EnsureLoadedAsync(ct).ConfigureAwait(false);
            entries.Clear();
            await WriteAsync(entries, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();

    [LoggerMessage(Level = LogLevel.Error, Message = "Echec d'acces a l'historique")]
    private static partial void LogIoFailed(ILogger logger, Exception exception);

    private async Task<List<HistoryEntry>> EnsureLoadedAsync(CancellationToken ct)
    {
        if (_entries is not null)
        {
            return _entries;
        }

        try
        {
            var document = await _store.ReadAsync(_file, AppJsonContext.Default.HistoryDocument, ct).ConfigureAwait(false);
            _entries = [.. document?.Entries ?? []];
        }
        catch (StoreCorruptedException)
        {
            _resetPending = true;
            _entries = [];
        }
        catch (IOException ex)
        {
            LogIoFailed(_logger, ex);
            _entries = [];
        }

        return _entries;
    }

    private async Task WriteAsync(List<HistoryEntry> entries, CancellationToken ct)
    {
        try
        {
            await _store.WriteAsync(_file, new HistoryDocument(1, [.. entries]), AppJsonContext.Default.HistoryDocument, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogIoFailed(_logger, ex);
        }
    }
}
