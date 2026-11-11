namespace WindowsCompanion.Models;

/// <summary>Scope of an HTML report (F-EXP-01).</summary>
public enum ExportKind
{
    /// <summary>Hardware page.</summary>
    Hardware,

    /// <summary>System page.</summary>
    System,

    /// <summary>Network page.</summary>
    Network,

    /// <summary>Full report: hardware, system, network, pending updates.</summary>
    Full,
}
