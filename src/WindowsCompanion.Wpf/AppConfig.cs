using System.Windows.Media;

namespace WindowsCompanion;

/// <summary>Application constants (rules/config.md). No brush, size or animation duration here: they live in Themes/Tokens.xaml.</summary>
public static class AppConfig
{
    // Application
    /// <summary>Technical identifier: namespace, data folder, debug variable.</summary>
    public const string AppName = "WindowsCompanion";

    /// <summary>Display name: window title, product name.</summary>
    public const string AppDisplayName = "Windows Companion";

    /// <summary>Version, mirror of the .csproj &lt;Version&gt; (rules/versioning.md).</summary>
    public const string AppVersion = "1.0.0";

    // Accent (design-system.md §2) - fallback when the Windows accent cannot be read
    /// <summary>Fallback accent, the default Windows blue.</summary>
    public const string AccentColor = "#0067C0";

    /// <summary>Gets the parsed fallback accent.</summary>
    public static Color AccentColorValue { get; } = (Color)ColorConverter.ConvertFromString(AccentColor)!;

    // Data files - relative to %APPDATA%\WindowsCompanion
    /// <summary>Settings file.</summary>
    public const string PreferencesFileName = "preferences.json";

    /// <summary>History file.</summary>
    public const string HistoryFileName = "history.json";

    // Culture - French only
    /// <summary>Interface culture.</summary>
    public const string DefaultCulture = "fr-FR";

    // Window (cahier 3.2, D-04)
    /// <summary>Minimum width.</summary>
    public const double WindowMinWidth = 760;

    /// <summary>Minimum height.</summary>
    public const double WindowMinHeight = 600;

    /// <summary>Width on first launch.</summary>
    public const double WindowDefaultWidth = 1280;

    /// <summary>Height on first launch.</summary>
    public const double WindowDefaultHeight = 800;

    /// <summary>Window width at or below which the pane folds (F-NAV-07).</summary>
    public const double NavCollapseThreshold = 900;

    /// <summary>Window width above which cards use two columns and tiles four (cahier 3.3).</summary>
    public const double WideLayoutThreshold = 1100;

    // Toasts - layout.md §5, D-01: lifetimes are read by the ViewModel
    /// <summary>Toast position.</summary>
    public const string SnackbarPosition = "bottom-right";

    /// <summary>Toasts visible at most (TS-13).</summary>
    public const int ToastMaxVisible = 4;

    /// <summary>Lifetime of a success toast.</summary>
    public const int ToastSuccessMs = 4500;

    /// <summary>Lifetime of an information toast.</summary>
    public const int ToastInfoMs = 6000;

    /// <summary>Lifetime of a warning toast.</summary>
    public const int ToastWarningMs = 9000;

    /// <summary>Window in which a repeated message replaces the shown one (TS-49).</summary>
    public const int ToastDuplicateWindowMs = 2000;

    /// <summary>Timer resolution of the toasts.</summary>
    public const int ToastTickMs = 50;

    /// <summary>Delay before a closing toast leaves the list (exit animation).</summary>
    public const int ToastExitDelayMs = 200;

    // Logging - rules/logging.md
    /// <summary>Minimum log level.</summary>
    public const string LogLevel = "Information";

    /// <summary>Size of a log file before rolling.</summary>
    public const long LogMaxBytes = 1_000_000;

    /// <summary>Log files kept (D-06: 14 days).</summary>
    public const int LogRetainedFileCount = 14;

    // Behavior
    /// <summary>History entries kept (F-HIS-10).</summary>
    public const int HistoryMaxEntries = 1000;

    /// <summary>winget log lines kept (F-MAJ-43).</summary>
    public const int WingetLogMaxLines = 2000;

    /// <summary>Age after which the system page reads again (F-SYS-03).</summary>
    public const int SystemStaleMinutes = 5;

    /// <summary>Time to confirm the history clearing (F-HIS-07).</summary>
    public const int ClearConfirmMs = 3000;

    /// <summary>Wait for an open application to close (F-MAJ-24).</summary>
    public const int CloseAppWaitMs = 10000;

    /// <summary>Uptime from which a restart is advised (F-ACC-13).</summary>
    public const int RestartAdviceDays = 7;

    /// <summary>Usage from which a volume is almost full.</summary>
    public const double UsageAttentionPercent = 80;

    /// <summary>Usage from which a volume is full.</summary>
    public const double UsageCriticalPercent = 90;

    /// <summary>Number of application tile colors (AppTile0Brush to AppTile9Brush).</summary>
    public const int AppTileColorCount = 10;

    // External programs and probes
    /// <summary>winget, resolved through the user's PATH (App Execution Alias).</summary>
    public const string WingetExecutable = "winget";

    /// <summary>Gateway ping timeout.</summary>
    public const int GatewayPingTimeoutMs = 2000;

    /// <summary>DNS test timeout.</summary>
    public const int DnsTimeoutMs = 3000;

    /// <summary>Internet test timeout.</summary>
    public const int InternetTimeoutMs = 3000;

    /// <summary>Name resolved by the DNS test.</summary>
    public const string DnsTestHost = "github.com";

    /// <summary>Windows connectivity probe.</summary>
    public const string InternetTestUrl = "http://www.msftconnecttest.com/connecttest.txt";

    /// <summary>Expected answer of the probe.</summary>
    public const string InternetTestExpected = "Microsoft Connect Test";

    /// <summary>Processor usage sampling window.</summary>
    public const int CpuSampleMs = 1000;

    /// <summary>Timeout of one step of a CIM enumeration. A step that times out ends the enumeration silently, so it is
    /// sized for the slow providers (licensing, signed drivers) when every startup query runs at once.</summary>
    public const int CimTimeoutMs = 10000;

    // Single instance (F-FEN-07)
    /// <summary>Name of the single-instance mutex.</summary>
    public const string SingleInstanceMutexName = @"Local\WindowsCompanion.SingleInstance";

    /// <summary>Name of the activation event.</summary>
    public const string ActivationEventName = @"Local\WindowsCompanion.Activate";

    /// <summary>Wait for the replaced instance when relaunching as administrator.</summary>
    public const int ReplaceWaitMs = 10000;
}
