namespace WindowsCompanion.Models;

/// <summary>Computed state of a value, which drives its color; never derived from the text (cahier E13).</summary>
public enum InfoState
{
    /// <summary>No particular state.</summary>
    Neutral,

    /// <summary>Healthy security or hardware state.</summary>
    Good,

    /// <summary>Needs attention.</summary>
    Warning,
}

/// <summary>The six cards of the System page, in display order.</summary>
public enum SystemGroupKind
{
    /// <summary>Operating system.</summary>
    OperatingSystem,

    /// <summary>Device.</summary>
    Device,

    /// <summary>Security.</summary>
    Security,

    /// <summary>Session and startup.</summary>
    Session,

    /// <summary>Software and components.</summary>
    Software,

    /// <summary>Drivers and services.</summary>
    Drivers,
}

/// <summary>A label/value line of an information block (pages and exports).</summary>
/// <param name="Label">Label.</param>
/// <param name="Value">Formatted value; null renders as "not available".</param>
/// <param name="State">Computed state, drives the color.</param>
/// <param name="RequiresAdmin">Whether the null value is due to missing administrator rights.</param>
/// <param name="IsMono">Whether the value is technical and shown in the monospace font.</param>
public sealed record InfoRow(
    string Label,
    string? Value,
    InfoState State = InfoState.Neutral,
    bool RequiresAdmin = false,
    bool IsMono = false)
{
    /// <summary>Gets the text shown: the value, or why it is missing (F-MAT-06, F-SYS-04).</summary>
    public string DisplayValue => Value ?? (RequiresAdmin ? Resources.Strings.Common_AdminRequired : Resources.Strings.Common_NotAvailable);

    /// <summary>Gets a value indicating whether the value is missing (shown in the tertiary color).</summary>
    public bool IsMissing => Value is null;
}

/// <summary>A card of the System page.</summary>
/// <param name="Kind">Which card.</param>
/// <param name="Rows">Lines, in display order.</param>
public sealed record SystemGroup(SystemGroupKind Kind, IReadOnlyList<InfoRow> Rows);

/// <summary>Security states used by the recommended actions; null means unreadable.</summary>
/// <param name="DefenderActive">Microsoft Defender real-time protection.</param>
/// <param name="FirewallActive">Windows Firewall, all profiles.</param>
/// <param name="SecureBoot">Secure Boot.</param>
/// <param name="BitLockerSystem">BitLocker on the system volume (null without administrator rights).</param>
public sealed record SecuritySummary(bool? DefenderActive, bool? FirewallActive, bool? SecureBoot, bool? BitLockerSystem);

/// <summary>System snapshot (cahier 5.6).</summary>
/// <param name="TakenAt">Time of the reading.</param>
/// <param name="DeviceName">Device name.</param>
/// <param name="Edition">Windows edition, for example "Windows 11 Professionnel".</param>
/// <param name="DisplayVersion">Edition version, for example 25H2.</param>
/// <param name="BootTime">Last boot time.</param>
/// <param name="Groups">The six cards.</param>
/// <param name="Security">Security states.</param>
public sealed record SystemSnapshot(
    DateTimeOffset TakenAt,
    string DeviceName,
    string Edition,
    string? DisplayVersion,
    DateTimeOffset? BootTime,
    IReadOnlyList<SystemGroup> Groups,
    SecuritySummary Security);
