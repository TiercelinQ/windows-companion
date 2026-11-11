namespace WindowsCompanion.Models;

/// <summary>The seven pages of the navigation pane.</summary>
public enum PageKey
{
    /// <summary>Overview (start page).</summary>
    Overview,

    /// <summary>Application updates.</summary>
    Updates,

    /// <summary>Hardware sheet.</summary>
    Hardware,

    /// <summary>System sheet.</summary>
    System,

    /// <summary>Network sheet.</summary>
    Network,

    /// <summary>Installation history.</summary>
    History,

    /// <summary>Settings (pane footer).</summary>
    Settings,
}

/// <summary>Stable page identifiers, used by the <c>--page={id}</c> argument and the navigation tags.</summary>
public static class PageKeys
{
    /// <summary>Identifier of the overview.</summary>
    public const string OverviewId = "accueil";

    /// <summary>Identifier of the updates page.</summary>
    public const string UpdatesId = "mises-a-jour";

    /// <summary>Identifier of the hardware page.</summary>
    public const string HardwareId = "materiel";

    /// <summary>Identifier of the system page.</summary>
    public const string SystemId = "systeme";

    /// <summary>Identifier of the network page.</summary>
    public const string NetworkId = "reseau";

    /// <summary>Identifier of the history page.</summary>
    public const string HistoryId = "historique";

    /// <summary>Identifier of the settings page.</summary>
    public const string SettingsId = "parametres";

    private static readonly Dictionary<PageKey, string> Ids = new()
    {
        [PageKey.Overview] = OverviewId,
        [PageKey.Updates] = UpdatesId,
        [PageKey.Hardware] = HardwareId,
        [PageKey.System] = SystemId,
        [PageKey.Network] = NetworkId,
        [PageKey.History] = HistoryId,
        [PageKey.Settings] = SettingsId,
    };

    /// <summary>Gets the identifier of a page.</summary>
    /// <param name="key">The page.</param>
    /// <returns>The identifier, for example <c>mises-a-jour</c>.</returns>
    public static string ToId(PageKey key) => Ids[key];

    /// <summary>Reads a page identifier; an unknown or missing identifier opens the overview (F-NAV-10).</summary>
    /// <param name="id">The identifier, possibly null.</param>
    /// <returns>The matching page, or <see cref="PageKey.Overview"/>.</returns>
    public static PageKey Parse(string? id)
    {
        foreach (var (key, value) in Ids)
        {
            if (string.Equals(value, id, StringComparison.OrdinalIgnoreCase))
            {
                return key;
            }
        }

        return PageKey.Overview;
    }

    /// <summary>Reads the <c>--page={id}</c> argument from the command line.</summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>The requested page, or <see cref="PageKey.Overview"/>.</returns>
    public static PageKey FromArguments(IEnumerable<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        const string Prefix = "--page=";
        var arg = args.FirstOrDefault(a => a.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase));
        return Parse(arg?[Prefix.Length..]);
    }
}
