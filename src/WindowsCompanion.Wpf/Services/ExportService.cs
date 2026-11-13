using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using WindowsCompanion.Helpers;
using WindowsCompanion.Models;
using WindowsCompanion.Resources;

namespace WindowsCompanion.Services;

/// <summary>Content of a report; only the parts matching the report kind are written.</summary>
/// <param name="Hardware">Hardware snapshot.</param>
/// <param name="System">System snapshot.</param>
/// <param name="Network">Network snapshot.</param>
/// <param name="Updates">Pending updates (full report).</param>
public sealed record ReportContent(HardwareSnapshot? Hardware, SystemSnapshot? System, NetworkSnapshot? Network, IReadOnlyList<UpdateItem>? Updates);

/// <summary>Reports, history CSV, text summary (cahier 4.10).</summary>
public interface IExportService
{
    /// <summary>Writes a self-contained HTML report in Documents, never overwriting a file (F-EXP-01/02).</summary>
    /// <param name="kind">Report scope.</param>
    /// <param name="content">Data to write.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The full path of the file, or a failure (N-27).</returns>
    Task<Result<string>> ExportReportAsync(ExportKind kind, ReportContent content, CancellationToken ct);

    /// <summary>Writes the history as CSV: UTF-8 with BOM, semicolon separator (F-EXP-04).</summary>
    /// <param name="entries">Entries, most recent first.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The full path of the file, or a failure (N-27).</returns>
    Task<Result<string>> ExportHistoryCsvAsync(IReadOnlyList<HistoryEntry> entries, CancellationToken ct);

    /// <summary>Builds the one-line summary copied by "Copier le résumé" (F-EXP-06).</summary>
    /// <param name="hardware">Hardware snapshot.</param>
    /// <param name="system">System snapshot.</param>
    /// <returns>The summary.</returns>
    string BuildSummary(HardwareSnapshot? hardware, SystemSnapshot? system);

    /// <summary>Opens the Explorer on a file, selected.</summary>
    /// <param name="path">Full path of the file.</param>
    void RevealInExplorer(string path);

    /// <summary>Opens the Explorer on the folder of the log files, creating it when it does not exist yet.</summary>
    void OpenLogsFolder();
}

/// <summary>File-system implementation of <see cref="IExportService"/>.</summary>
/// <param name="paths">Application locations.</param>
/// <param name="runner">Starts the Explorer.</param>
/// <param name="time">Clock.</param>
/// <param name="logger">Logger.</param>
public sealed partial class ExportService(IAppPaths paths, IProcessRunner runner, TimeProvider time, ILogger<ExportService> logger) : IExportService
{
    private const string FilePrefix = "windows-companion";
    private const string CssResource = "WindowsCompanion.Resources.report.css";

    /// <inheritdoc />
    public async Task<Result<string>> ExportReportAsync(ExportKind kind, ReportContent content, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(content);
        var date = time.GetLocalNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var name = kind switch
        {
            ExportKind.Full => $"{FilePrefix}-{date}",
            _ => $"{FilePrefix}-{PageKeys.ToId(kind switch { ExportKind.Hardware => PageKey.Hardware, ExportKind.System => PageKey.System, _ => PageKey.Network })}-{date}",
        };

        return await WriteAsync(name, ".html", BuildHtml(kind, content), withBom: false, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result<string>> ExportHistoryCsvAsync(IReadOnlyList<HistoryEntry> entries, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var csv = new StringBuilder();
        csv.AppendLine(string.Join(';', Strings.Csv_Date, Strings.Csv_Application, Strings.Csv_Id, Strings.Csv_FromVersion, Strings.Csv_ToVersion, Strings.Csv_Result, Strings.Csv_ExitCode, Strings.Csv_Duration));
        foreach (var e in entries)
        {
            csv.AppendLine(string.Join(
                ';',
                e.Timestamp.ToLocalTime().ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture),
                Csv(e.AppName),
                Csv(e.AppId),
                Csv(e.FromVersion ?? string.Empty),
                Csv(e.ToVersion),
                Csv(InfoRows.HistoryResultLabel(e.Result)),
                Csv(e.ExitCode ?? string.Empty),
                e.DurationSeconds?.ToString(CultureInfo.InvariantCulture) ?? string.Empty));
        }

        var date = time.GetLocalNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return await WriteAsync($"historique-{FilePrefix}-{date}", ".csv", csv.ToString(), withBom: true, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public string BuildSummary(HardwareSnapshot? hardware, SystemSnapshot? system)
    {
        var parts = new List<string>();
        if (system is not null)
        {
            parts.Add(system.DeviceName);
            parts.Add(string.Join(' ', new[] { ShortEdition(system.Edition), system.DisplayVersion }.Where(p => !string.IsNullOrEmpty(p))));
        }

        if (hardware is not null)
        {
            if (hardware.Cpu.Model is { } cpu)
            {
                parts.Add(cpu);
            }

            parts.Add(Formats.Template(Strings.Summary_Memory, Formats.Gb(hardware.Memory.InstalledBytes ?? hardware.Memory.VisibleBytes, 0)));
            parts.Add(Formats.Plural(hardware.PhysicalDiskCount, Strings.Summary_DiskOne, Strings.Summary_DiskMany));
        }

        return string.Join(" · ", parts);
    }

    /// <inheritdoc />
    public void RevealInExplorer(string path) =>
        runner.Start(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), ["/select," + path]);

    /// <inheritdoc />
    public void OpenLogsFolder()
    {
        // Created when missing: explorer.exe falls back to Documents on a folder that does not exist.
        Directory.CreateDirectory(paths.Logs);
        runner.Start(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), [paths.Logs]);
    }

    private static string ShortEdition(string edition) => edition.Replace(Strings.Summary_EditionPro, Strings.Summary_EditionProShort, StringComparison.Ordinal);

    private static string Csv(string value) =>
        value.Contains(';', StringComparison.Ordinal) || value.Contains('"', StringComparison.Ordinal)
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;

    private static string Css()
    {
        using var stream = typeof(ExportService).Assembly.GetManifestResourceStream(CssResource);
        if (stream is null)
        {
            return string.Empty;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static void Section(StringBuilder html, string title, IEnumerable<InfoRow> rows)
    {
        html.Append("<section><h2>").Append(WebUtility.HtmlEncode(title)).Append("</h2><table>");
        foreach (var row in rows)
        {
            html.Append("<tr><th>").Append(WebUtility.HtmlEncode(row.Label)).Append("</th><td>")
                .Append(WebUtility.HtmlEncode(InfoRows.ValueText(row))).Append("</td></tr>");
        }

        html.Append("</table></section>");
    }

    private string BuildHtml(ExportKind kind, ReportContent content)
    {
        var title = kind switch
        {
            ExportKind.Hardware => Strings.Report_TitleHardware,
            ExportKind.System => Strings.Report_TitleSystem,
            ExportKind.Network => Strings.Report_TitleNetwork,
            _ => Strings.Report_TitleFull,
        };

        var html = new StringBuilder();
        html.Append("<!doctype html><html lang=\"fr\"><head><meta charset=\"utf-8\"><title>")
            .Append(WebUtility.HtmlEncode(title)).Append("</title><style>").Append(Css()).Append("</style></head><body><header><h1>")
            .Append(WebUtility.HtmlEncode(title)).Append("</h1><p>")
            .Append(WebUtility.HtmlEncode(Formats.Template(Strings.Report_Generated, Formats.DateTime(time.GetLocalNow().DateTime))))
            .Append("</p></header>");

        if (kind is ExportKind.Hardware or ExportKind.Full && content.Hardware is { } hw)
        {
            Section(html, Strings.Hw_Processor, InfoRows.Cpu(hw.Cpu));
            Section(html, Strings.Hw_Memory, InfoRows.Memory(hw.Memory));
            foreach (var gpu in hw.Gpus)
            {
                Section(html, Strings.Hw_Graphics, InfoRows.Gpu(gpu, hw.Displays));
            }

            Section(html, Strings.Hw_Board, InfoRows.Board(hw.Board));
            foreach (var volume in hw.Volumes)
            {
                Section(html, InfoRows.VolumeTitle(volume), InfoRows.Volume(volume));
            }
        }

        if (kind is ExportKind.System or ExportKind.Full && content.System is { } sys)
        {
            foreach (var group in sys.Groups)
            {
                Section(html, InfoRows.GroupTitle(group.Kind), group.Rows);
            }
        }

        if (kind is ExportKind.Network or ExportKind.Full && content.Network is { } net)
        {
            foreach (var adapter in net.Adapters)
            {
                Section(html, adapter.Name, InfoRows.Adapter(adapter, maskMac: true));
            }
        }

        if (kind == ExportKind.Full && content.Updates is { Count: > 0 } updates)
        {
            html.Append("<section><h2>").Append(WebUtility.HtmlEncode(Strings.Nav_Updates)).Append("</h2><table><tr><th>")
                .Append(WebUtility.HtmlEncode(Strings.Upd_ColApplication)).Append("</th><th>")
                .Append(WebUtility.HtmlEncode(Strings.Upd_ColInstalled)).Append("</th><th>")
                .Append(WebUtility.HtmlEncode(Strings.Upd_ColAvailable)).Append("</th><th>")
                .Append(WebUtility.HtmlEncode(Strings.Upd_ColSource)).Append("</th></tr>");
            foreach (var u in updates)
            {
                html.Append("<tr><td>").Append(WebUtility.HtmlEncode($"{u.Name} ({u.Id})")).Append("</td><td>")
                    .Append(WebUtility.HtmlEncode(u.InstalledVersion ?? Strings.Upd_VersionUnknown)).Append("</td><td>")
                    .Append(WebUtility.HtmlEncode(u.AvailableVersion)).Append("</td><td>")
                    .Append(WebUtility.HtmlEncode(InfoRows.SourceLabel(u.Source))).Append("</td></tr>");
            }

            html.Append("</table></section>");
        }

        html.Append("</body></html>");
        return html.ToString();
    }

    private async Task<Result<string>> WriteAsync(string baseName, string extension, string text, bool withBom, CancellationToken ct)
    {
        try
        {
            var path = UniquePath(baseName, extension);
            await File.WriteAllTextAsync(path, text, new UTF8Encoding(withBom), ct).ConfigureAwait(false);
            return Result<string>.Success(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogExportFailed(logger, ex);
            return Result<string>.Failure(new(FeedbackType.Danger, Strings.Toast_ExportFailed_Title, Formats.Template(Strings.Toast_ExportFailed_Message, ex.Message)));
        }
    }

    // Never overwrite: suffix -2, -3... (F-EXP-02).
    private string UniquePath(string baseName, string extension)
    {
        var path = Path.Combine(paths.Documents, baseName + extension);
        for (var i = 2; File.Exists(path); i++)
        {
            path = Path.Combine(paths.Documents, $"{baseName}-{i.ToString(CultureInfo.InvariantCulture)}{extension}");
        }

        return path;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Echec de l'export")]
    private static partial void LogExportFailed(ILogger logger, Exception exception);
}
