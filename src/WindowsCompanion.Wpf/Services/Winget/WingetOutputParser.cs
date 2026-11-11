using System.Globalization;
using System.Text.RegularExpressions;
using WindowsCompanion.Models;

namespace WindowsCompanion.Services.Winget;

/// <summary>Reads the text output of the winget command line (risk R2).</summary>
/// <remarks>
/// The update table is read by column positions, taken from the header line above the line of dashes.
/// The column order (name, identifier, version, available, source) does not depend on the Windows language.
/// </remarks>
public static partial class WingetOutputParser
{
    private const int MinDashLength = 10;
    private const uint WingetErrorBase = 0x8A150000;

    /// <summary>Removes the console control sequences and trailing spaces of a line.</summary>
    /// <param name="line">Raw line.</param>
    /// <returns>Cleaned line.</returns>
    public static string Clean(string line) => ControlSequence().Replace(line ?? string.Empty, string.Empty).Trim('\r').TrimEnd();

    /// <summary>Tells whether a cleaned line carries no information for the log: blank, spinner or progress bar.</summary>
    /// <param name="line">Cleaned line.</param>
    /// <returns>True for noise.</returns>
    public static bool IsNoise(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Length == 0
            || trimmed.All(c => c is '-' or '\\' or '|' or '/')
            || trimmed.Contains('█', StringComparison.Ordinal)
            || trimmed.Contains('▒', StringComparison.Ordinal);
    }

    /// <summary>Reads the download progress of a line, from "12.5 MB / 78.0 MB" or from a percentage.</summary>
    /// <param name="line">Cleaned line.</param>
    /// <returns>A percentage, or null when the line reports no progress.</returns>
    public static double? ParseDownloadPercent(string line)
    {
        var sizes = SizeFraction().Match(line);
        if (sizes.Success
            && TryNumber(sizes.Groups["done"].Value, out var done)
            && TryNumber(sizes.Groups["total"].Value, out var total)
            && total > 0)
        {
            var ratio = done * Unit(sizes.Groups["doneUnit"].Value) / (total * Unit(sizes.Groups["totalUnit"].Value));
            return Math.Clamp(ratio * 100, 0, 100);
        }

        var percent = Percent().Match(line);
        return percent.Success && TryNumber(percent.Groups["value"].Value, out var value) ? Math.Clamp(value, 0, 100) : null;
    }

    /// <summary>Reads the update table of <c>winget upgrade</c>.</summary>
    /// <param name="output">Standard output.</param>
    /// <param name="source">Source that was searched.</param>
    /// <returns>The items; empty when no table is present.</returns>
    public static IReadOnlyList<UpdateItem> ParseUpgradeTable(string output, UpdateSource source)
    {
        var lines = (output ?? string.Empty).Split('\n').Select(Clean).ToList();
        var dash = lines.FindIndex(l => l.Length >= MinDashLength && l.All(c => c == '-'));
        if (dash < 1)
        {
            return [];
        }

        var starts = ColumnStarts(lines[dash - 1]);
        if (starts.Count < 4)
        {
            return [];
        }

        var items = new List<UpdateItem>();
        for (var i = dash + 1; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.Length <= starts[3])
            {
                break; // blank line or footer ("3 upgrades available.")
            }

            var name = Field(line, starts[0], starts[1]);
            var id = Field(line, starts[1], starts[2]);
            var installed = Field(line, starts[2], starts[3]);
            var available = Field(line, starts[3], starts.Count > 4 ? starts[4] : line.Length);
            if (name.Length == 0 || id.Length == 0 || id.Contains(' ', StringComparison.Ordinal) || available.Length == 0)
            {
                break;
            }

            if (items.Exists(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            items.Add(new UpdateItem(id, name, installed.Length == 0 ? null : installed, available, source, null));
        }

        return items;
    }

    /// <summary>Tells whether a search that printed no table ended normally (no update, no installed package).</summary>
    /// <param name="exitCode">Exit code of <c>winget upgrade</c>.</param>
    /// <returns>True when the empty result is not an error.</returns>
    public static bool IsEmptyResult(int exitCode) =>
        exitCode is 0 or unchecked((int)0x8A150014) or unchecked((int)0x8A15002B);

    /// <summary>Formats an exit code: hexadecimal for a winget code, decimal for an installer code.</summary>
    /// <param name="exitCode">Exit code.</param>
    /// <returns>For example <c>1603</c> or <c>0x8A150011</c>.</returns>
    public static string FormatExitCode(int exitCode) =>
        unchecked((uint)exitCode) >= WingetErrorBase
            ? "0x" + unchecked((uint)exitCode).ToString("X8", CultureInfo.InvariantCulture)
            : exitCode.ToString(CultureInfo.InvariantCulture);

    /// <summary>Reads the version printed by <c>winget --version</c>.</summary>
    /// <param name="output">Standard output.</param>
    /// <returns>The version without its leading "v", or null.</returns>
    public static string? ParseVersion(string output)
    {
        var line = (output ?? string.Empty).Split('\n').Select(Clean).FirstOrDefault(l => l.Length > 0);
        return line?.TrimStart('v', 'V');
    }

    private static List<int> ColumnStarts(string header)
    {
        var starts = new List<int>();
        for (var i = 0; i < header.Length; i++)
        {
            if (header[i] != ' ' && (i == 0 || header[i - 1] == ' '))
            {
                starts.Add(i);
            }
        }

        return starts;
    }

    private static string Field(string line, int start, int end)
    {
        if (start >= line.Length)
        {
            return string.Empty;
        }

        var length = Math.Min(end, line.Length) - start;
        return length <= 0 ? string.Empty : line.Substring(start, length).Trim();
    }

    private static bool TryNumber(string text, out double value) =>
        double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static double Unit(string unit) => unit.ToUpperInvariant() switch
    {
        "KB" => 1024d,
        "MB" => 1024d * 1024,
        "GB" => 1024d * 1024 * 1024,
        _ => 1d,
    };

    [GeneratedRegex(@"\x1B\[[0-?]*[ -/]*[@-~]|\x1B\][^\x07]*\x07")]
    private static partial Regex ControlSequence();

    [GeneratedRegex(@"(?<done>\d+(?:[.,]\d+)?)\s*(?<doneUnit>B|KB|MB|GB)\s*/\s*(?<total>\d+(?:[.,]\d+)?)\s*(?<totalUnit>B|KB|MB|GB)", RegexOptions.IgnoreCase)]
    private static partial Regex SizeFraction();

    [GeneratedRegex(@"(?<value>\d{1,3}(?:[.,]\d+)?)\s*%")]
    private static partial Regex Percent();
}
