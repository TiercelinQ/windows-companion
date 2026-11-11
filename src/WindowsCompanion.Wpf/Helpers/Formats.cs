using System.Globalization;
using System.Text;
using WindowsCompanion.Resources;

namespace WindowsCompanion.Helpers;

/// <summary>Display formats of the interface (cahier 7.5): decimal comma, narrow no-break thousands separator, no-break space before units.</summary>
public static class Formats
{
    private const char NoBreakSpace = ' ';
    private const string ThinNoBreakSpace = " ";
    private const double MiB = 1024d * 1024;
    private const double GiB = 1024d * 1024 * 1024;
    private const int SecondsPerMinute = 60;
    private const int SecondsPerHour = 3600;
    private const int MacHexLength = 12;

    /// <summary>Gets the culture of the interface, with the narrow no-break thousands separator.</summary>
    public static CultureInfo Culture { get; } = BuildCulture();

    /// <summary>Formats a resource template.</summary>
    /// <param name="format">Template with {0}-style placeholders.</param>
    /// <param name="args">Values.</param>
    /// <returns>The text.</returns>
    public static string Template(string format, params object?[] args) =>
        string.Format(Culture, CompositeFormat.Parse(format), args);

    /// <summary>Chooses the singular (0 and 1) or plural template and formats the count (cahier 7.3).</summary>
    /// <param name="count">Count.</param>
    /// <param name="one">Singular template, {0} = count.</param>
    /// <param name="many">Plural template, {0} = count.</param>
    /// <returns>The text.</returns>
    public static string Plural(int count, string one, string many) => Template(count <= 1 ? one : many, Whole(count));

    /// <summary>Formats an integer with the thousands separator.</summary>
    /// <param name="value">Value.</param>
    /// <returns>For example "1 190".</returns>
    public static string Whole(long value) => value.ToString("#,0", Culture);

    /// <summary>Formats a number rounded to the nearest integer.</summary>
    /// <param name="value">Value.</param>
    /// <returns>For example "23".</returns>
    public static string Number(double value) => Math.Round(value, MidpointRounding.AwayFromZero).ToString("#,0", Culture);

    /// <summary>Formats a percentage rounded to the nearest integer.</summary>
    /// <param name="value">Percentage.</param>
    /// <returns>For example "66 %".</returns>
    public static string Percent(double value) => Number(value) + NoBreakSpace + "%";

    /// <summary>Formats a download size: Mo below 1 024 Mo (one decimal under 10 Mo), else Go with one decimal.</summary>
    /// <param name="bytes">Size in bytes.</param>
    /// <returns>For example "1,6 Mo", "78 Mo", "1,4 Go".</returns>
    public static string Size(long bytes)
    {
        var mb = bytes / MiB;
        if (mb < 1024)
        {
            return (mb < 10 ? mb.ToString("0.0", Culture) : Math.Round(mb).ToString("#,0", Culture)) + NoBreakSpace + Strings.Unit_Mb;
        }

        return (bytes / GiB).ToString("#,0.0", Culture) + NoBreakSpace + Strings.Unit_Gb;
    }

    /// <summary>Formats a number of gigabytes (1 073 741 824 bytes, Explorer convention), without unit.</summary>
    /// <param name="bytes">Size in bytes.</param>
    /// <param name="decimals">Decimals: 0 for a disk, 1 for memory.</param>
    /// <returns>For example "931" or "31,7".</returns>
    public static string Gb(long bytes, int decimals) =>
        (bytes / GiB).ToString(decimals == 0 ? "#,0" : "#,0." + new string('0', decimals), Culture);

    /// <summary>Formats a total to download, "or more" when a size is unknown (cahier E3).</summary>
    /// <param name="bytes">Known bytes.</param>
    /// <param name="partial">Whether some sizes are unknown.</param>
    /// <returns>For example "1,4 Go" or "1,4 Go ou plus".</returns>
    public static string DownloadTotal(long bytes, bool partial) =>
        Template(partial ? Strings.Size_GbOrMore : Strings.Size_Gb, Gb(bytes, 1));

    /// <summary>Formats a frequency given in MHz.</summary>
    /// <param name="megahertz">Frequency.</param>
    /// <returns>For example "2,40 GHz".</returns>
    public static string Ghz(double megahertz) => (megahertz / 1000).ToString("0.00", Culture) + NoBreakSpace + Strings.Unit_Ghz;

    /// <summary>Formats a date alone.</summary>
    /// <param name="date">Date.</param>
    /// <returns>dd/MM/yyyy.</returns>
    public static string Date(DateTime date) => date.ToString("dd/MM/yyyy", Culture);

    /// <summary>Formats an absolute date and time.</summary>
    /// <param name="date">Date.</param>
    /// <returns>dd/MM/yyyy HH:mm.</returns>
    public static string DateTime(DateTime date) => date.ToString("dd/MM/yyyy HH:mm", Culture);

    /// <summary>Formats a relative date: today, yesterday, else day and abbreviated month, with the year when it differs.</summary>
    /// <param name="at">Date to format.</param>
    /// <param name="now">Current time.</param>
    /// <param name="capitalize">Whether the first letter is a capital (history) or not (inside a sentence).</param>
    /// <returns>For example "Aujourd'hui 08:12", "Hier 21:05", "3 nov. 10:40".</returns>
    public static string Relative(DateTimeOffset at, DateTimeOffset now, bool capitalize)
    {
        var local = at.ToLocalTime();
        var today = now.ToLocalTime().Date;
        var time = local.ToString("HH:mm", Culture);
        var text = local.Date == today
            ? Template(Strings.Date_Today, time)
            : local.Date == today.AddDays(-1)
                ? Template(Strings.Date_Yesterday, time)
                : local.ToString(local.Year == today.Year ? "d MMM HH:mm" : "d MMM yyyy HH:mm", Culture);
        return capitalize || text.Length == 0 ? text : char.ToLower(text[0], Culture) + text[1..];
    }

    /// <summary>Formats a processing duration.</summary>
    /// <param name="seconds">Duration.</param>
    /// <returns>"42 s", "3 min 10 s" or "1 h 05 min".</returns>
    public static string Duration(int seconds)
    {
        if (seconds < SecondsPerMinute)
        {
            return Template(Strings.Duration_Seconds, seconds);
        }

        if (seconds < SecondsPerHour)
        {
            return Template(Strings.Duration_Minutes, seconds / SecondsPerMinute, seconds % SecondsPerMinute);
        }

        return Template(Strings.Duration_Hours, seconds / SecondsPerHour, ((seconds % SecondsPerHour) / SecondsPerMinute).ToString("00", Culture));
    }

    /// <summary>Formats an uptime.</summary>
    /// <param name="uptime">Time since boot.</param>
    /// <returns>"12 jours 5 heures", or "5 heures" under a day.</returns>
    public static string Uptime(TimeSpan uptime)
    {
        var hours = Plural(uptime.Hours, Strings.Uptime_HourOne, Strings.Uptime_HourMany);
        return uptime.Days < 1 ? hours : Plural(uptime.Days, Strings.Uptime_DayOne, Strings.Uptime_DayMany) + " " + hours;
    }

    /// <summary>Formats the "started" part of the overview subtitle.</summary>
    /// <param name="uptime">Time since boot.</param>
    /// <returns>"démarré aujourd'hui", "démarré il y a 1 jour", "démarré il y a 12 jours".</returns>
    public static string StartedAgo(TimeSpan uptime) =>
        uptime.Days < 1 ? Strings.Started_Today : Plural(uptime.Days, Strings.Started_DayOne, Strings.Started_DayMany);

    /// <summary>Formats a link speed.</summary>
    /// <param name="bitsPerSecond">Speed, null when unknown.</param>
    /// <returns>For example "1 Gb/s", "2,5 Gb/s", "100 Mb/s"; empty when unknown.</returns>
    public static string LinkSpeed(long? bitsPerSecond) => bitsPerSecond switch
    {
        null or <= 0 => string.Empty,
        >= 1_000_000_000 => (bitsPerSecond.Value / 1e9).ToString("#,0.#", Culture) + NoBreakSpace + Strings.Unit_Gbps,
        _ => (bitsPerSecond.Value / 1e6).ToString("#,0.#", Culture) + NoBreakSpace + Strings.Unit_Mbps,
    };

    /// <summary>Formats a duration in milliseconds.</summary>
    /// <param name="milliseconds">Duration.</param>
    /// <returns>For example "23 ms".</returns>
    public static string Milliseconds(long milliseconds) => Whole(milliseconds) + NoBreakSpace + Strings.Unit_Ms;

    /// <summary>Formats a MAC address as uppercase hexadecimal pairs separated by dashes.</summary>
    /// <param name="raw">Address in any form ("3C5282...", "3c:52:...").</param>
    /// <returns>For example "3C-52-82-1A-9F-04", or null when not a MAC address.</returns>
    public static string? Mac(string? raw)
    {
        var hex = new string((raw ?? string.Empty).Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
        return hex.Length == MacHexLength
            ? string.Join('-', Enumerable.Range(0, MacHexLength / 2).Select(i => hex.Substring(i * 2, 2)))
            : null;
    }

    /// <summary>Joins items with commas and "et" before the last one.</summary>
    /// <param name="items">Items.</param>
    /// <returns>For example "a, b et c".</returns>
    public static string JoinWithAnd(IReadOnlyList<string> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return items.Count <= 1
            ? string.Concat(items)
            : string.Join(", ", items.Take(items.Count - 1)) + " " + Strings.Common_And + " " + items[^1];
    }

    private static CultureInfo BuildCulture()
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo(AppConfig.DefaultCulture).Clone();
        culture.NumberFormat.NumberGroupSeparator = ThinNoBreakSpace;
        return CultureInfo.ReadOnly(culture);
    }
}
