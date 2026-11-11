using System.Text.Json.Serialization;
using WindowsCompanion.Models;

namespace WindowsCompanion.Services.Data;

/// <summary>Content of <c>history.json</c>.</summary>
/// <param name="SchemaVersion">File format version.</param>
/// <param name="Entries">Entries, most recent first.</param>
public sealed record HistoryDocument(int SchemaVersion, IReadOnlyList<HistoryEntry> Entries);

/// <summary>Source-generated JSON context of the data files (trim-friendly, no reflection).</summary>
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(HistoryDocument))]
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
public sealed partial class AppJsonContext : JsonSerializerContext;
