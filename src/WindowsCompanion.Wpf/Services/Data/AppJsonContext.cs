using System.Text.Json.Serialization;
using WindowsCompanion.Models;

namespace WindowsCompanion.Services.Data;

/// <summary>Content of <c>history.json</c>.</summary>
/// <param name="SchemaVersion">File format version.</param>
/// <param name="Entries">Entries, most recent first.</param>
public sealed record HistoryDocument(int SchemaVersion, IReadOnlyList<HistoryEntry> Entries);

/// <summary>The part of a GitHub release read by the version check (D-13).</summary>
/// <param name="TagName">Release tag, for example <c>v1.0.2</c>.</param>
public sealed record GitHubRelease([property: JsonPropertyName("tag_name")] string? TagName);

/// <summary>Source-generated JSON context of the data files (trim-friendly, no reflection).</summary>
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(HistoryDocument))]
[JsonSerializable(typeof(GitHubRelease))]
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
public sealed partial class AppJsonContext : JsonSerializerContext;
