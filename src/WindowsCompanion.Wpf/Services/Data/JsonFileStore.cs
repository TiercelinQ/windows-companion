using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using WindowsCompanion.Models;

namespace WindowsCompanion.Services.Data;

/// <summary>Single access point to the JSON data files (rules/db.md, JSON mode).</summary>
public interface IJsonFileStore
{
    /// <summary>Reads a file. A missing file returns null; an unreadable file is renamed <c>.bak</c> and raises <see cref="StoreCorruptedException"/>.</summary>
    /// <typeparam name="T">Document type.</typeparam>
    /// <param name="path">Full path of the file.</param>
    /// <param name="typeInfo">Generated type metadata.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The document, or null when the file does not exist.</returns>
    Task<T?> ReadAsync<T>(string path, JsonTypeInfo<T> typeInfo, CancellationToken ct)
        where T : class;

    /// <summary>Writes a file atomically: temporary file in the same folder, then replacement (F-PER-02).</summary>
    /// <typeparam name="T">Document type.</typeparam>
    /// <param name="path">Full path of the file.</param>
    /// <param name="value">Document.</param>
    /// <param name="typeInfo">Generated type metadata.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task completed once the file is replaced.</returns>
    Task WriteAsync<T>(string path, T value, JsonTypeInfo<T> typeInfo, CancellationToken ct);
}

/// <summary>File-system implementation of <see cref="IJsonFileStore"/>.</summary>
/// <param name="logger">Logger.</param>
public sealed partial class JsonFileStore(ILogger<JsonFileStore> logger) : IJsonFileStore
{
    /// <inheritdoc />
    public async Task<T?> ReadAsync<T>(string path, JsonTypeInfo<T> typeInfo, CancellationToken ct)
        where T : class
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var stream = File.OpenRead(path);
            await using (stream.ConfigureAwait(false))
            {
                return await JsonSerializer.DeserializeAsync(stream, typeInfo, ct).ConfigureAwait(false)
                    ?? throw new JsonException("Document vide");
            }
        }
        catch (JsonException ex)
        {
            SetAside(path);
            LogCorrupted(logger, path, ex);
            throw new StoreCorruptedException($"Fichier illisible : {path}", ex);
        }
    }

    /// <inheritdoc />
    public async Task WriteAsync<T>(string path, T value, JsonTypeInfo<T> typeInfo, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        var stream = File.Create(temp);
        await using (stream.ConfigureAwait(false))
        {
            await JsonSerializer.SerializeAsync(stream, value, typeInfo, ct).ConfigureAwait(false);
        }

        File.Move(temp, path, overwrite: true);
    }

    // The unreadable file is kept as .bak for diagnosis; the caller starts again from defaults (F-PER-03).
    private static void SetAside(string path) => File.Move(path, path + ".bak", overwrite: true);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Fichier de donnees illisible, mis de cote en .bak : {Path}")]
    private static partial void LogCorrupted(ILogger logger, string path, Exception exception);
}
