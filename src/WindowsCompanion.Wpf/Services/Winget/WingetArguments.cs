using WindowsCompanion.Models;

namespace WindowsCompanion.Services.Winget;

/// <summary>Argument lists of the winget command line (T-09: one element per argument, never a command string).</summary>
public static class WingetArguments
{
    /// <summary>Gets the arguments that print the winget version.</summary>
    public static IReadOnlyList<string> Version { get; } = ["--version"];

    /// <summary>Gets the winget name of a source.</summary>
    /// <param name="source">The source.</param>
    /// <returns><c>winget</c> or <c>msstore</c>.</returns>
    public static string SourceName(UpdateSource source) => source == UpdateSource.MsStore ? "msstore" : "winget";

    /// <summary>Builds the arguments that list the updates of one source.</summary>
    /// <param name="source">Source to search.</param>
    /// <param name="includeUnknown">Whether to list applications with an unknown version.</param>
    /// <returns>The arguments.</returns>
    public static IReadOnlyList<string> Upgrade(UpdateSource source, bool includeUnknown)
    {
        List<string> args = ["upgrade", "--source", SourceName(source)];
        if (includeUnknown)
        {
            args.Add("--include-unknown");
        }

        args.AddRange(["--accept-source-agreements", "--disable-interactivity"]);
        return args;
    }

    /// <summary>Builds the arguments that install the update of one package.</summary>
    /// <param name="item">Item to update; its identifier stays a separate element.</param>
    /// <param name="silent">Whether the installer runs without a window.</param>
    /// <returns>The arguments.</returns>
    public static IReadOnlyList<string> Install(UpdateItem item, bool silent)
    {
        ArgumentNullException.ThrowIfNull(item);
        List<string> args = ["upgrade", "--id", item.Id, "--exact", "--source", SourceName(item.Source)];
        if (silent)
        {
            args.Add("--silent");
        }

        args.AddRange(["--accept-package-agreements", "--accept-source-agreements", "--disable-interactivity"]);
        return args;
    }

    /// <summary>Formats a command for the log, prefixed with <c>&gt; winget</c>.</summary>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The display line.</returns>
    public static string Display(IReadOnlyList<string> arguments) => "> winget " + string.Join(' ', arguments);
}
