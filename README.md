# Windows Companion - v1.2.0

## Objective

Windows 11 desktop application for PC maintenance. It searches, installs and cancels application updates (winget, Microsoft Store), and shows a hardware, system and network sheet that can be copied or exported.

It reads, installs and diagnoses. It changes no system setting beyond application updates and two explicit actions: enabling a network adapter and flushing the DNS cache.

## Features

- Shell: title bar (with the version next to the name), 7-page navigation pane (expanded or icon-only), status bar (winget version, last search, rights).
- Updates: search through winget and the Microsoft Store, source filter, live winget log, install one, a selection or everything, global progress by step, spinner and per-row state, cancel (waiting item, running item, whole queue), ignore list, "Application open" state with a Close button.
- Overview: four tiles, storage card, recommended actions, full report export.
- Hardware: processor, memory, graphics, motherboard and BIOS, storage volumes with usage gauges (80 % and 90 % thresholds), disk health and temperature, copy summary, HTML export.
- System: six cards, filter, copy summary, HTML export.
- Network: adapter blocks, four local connectivity tests, enable an adapter, flush DNS, HTML export.
- History: installation history, CSV export, two-step clear.
- Settings: sources, unknown versions, silent install, new-version check (automatic switch and "Rechercher maintenant" button), theme (System, Light, Dark), ignored applications.
- New-version check: at startup (once a day at most) and on demand, compares the installed version with the latest GitHub release and shows a toast with a "Télécharger" action that opens the releases page.
- Toasts: own host with four severities, up to two actions, stack of four, duplicate merge, pause on hover.
- Relaunch as administrator on the same page, single instance with activation of the open window, remembered window size and position, `F5` runs the main command of the page.

## Technical stack

- OS: Windows · Runtime: .NET 10 · C# 14 · WPF · WPF UI 4.3.0 (Fluent) · Icons: Segoe Fluent Icons
- Composition: IHost + Microsoft.Extensions.DependencyInjection · MVVM: CommunityToolkit.Mvvm 8.4.2
- Storage: JSON files under `%APPDATA%\WindowsCompanion` (`System.Text.Json`, source-generated context, atomic writes). No database.
- System access: `System.Management` (CIM/WMI), `LibraryImport` native calls, `winget.exe` through `Process` with an `ArgumentList`
- Network: `HttpClient` for the connectivity probe and the GitHub release check
- Logging: Serilog (rolling file under `%APPDATA%\WindowsCompanion\logs`)
- i18n: No (French only, single `Strings.resx`)
- Salesforce CLI: No
- Tests: Yes (xUnit v3, NSubstitute)

## Architecture

```
windows-companion/
├── WindowsCompanion.sln
├── Directory.Packages.props        central package versions
├── Directory.Build.props           net10.0-windows, C# 14, nullable, warnings as errors, analyzers
├── global.json                     test runner: Microsoft.Testing.Platform
├── src/WindowsCompanion.Wpf/
│   ├── App.xaml(.cs)               host, DI, Serilog, global handlers, single instance, theme, shell
│   ├── AppConfig.cs                application constants
│   ├── Assets/                     icon.ico (window, executable), icon.png (512 px)
│   ├── Models/                     DTOs, Result<T>, business exceptions
│   ├── Services/                   business logic, system access, winget, persistence (Data/, Winget/, Platform/)
│   ├── ViewModels/                 one per page, plus shell and toasts
│   ├── Views/                      7 pages, shell window, Layout/ (toast host, navigation)
│   ├── Converters/  Helpers/       presentation-only transforms, pure functions
│   ├── Resources/                  Strings.resx, report.css (HTML report styles)
│   └── Themes/                     Tokens.xaml, Styles.xaml
└── tests/WindowsCompanion.Wpf.Tests/
```

## Services and commands

| Area | Service | What it does |
|---|---|---|
| Updates | `IWingetService`, `IProcessRunner` | version, one `winget upgrade` call per source, install with live log, cancel by killing the process tree, open the App Installer page of the Microsoft Store |
| Updates | `IRunningAppService` | finds a running process under the package install location, soft close |
| Hardware, system | `IHardwareService`, `ISystemInfoService` | snapshots built from CIM, registry, native calls and DXGI |
| Network | `INetworkService` | adapters, gateway ping, DNS, internet check, enable adapter, flush DNS |
| Export | `IExportService` | HTML reports, history CSV, text summary, reveal in Explorer |
| Overview | `IRecommendationService` | recommended actions from the latest snapshots |
| App version | `IUpdateCheckService` | latest GitHub release compared with the installed version (daily at startup, or on demand), opens the releases page |
| Persistence | `IPreferencesService`, `IHistoryService`, `IJsonFileStore` | settings, exclusions, history (1,000 entries at most) |
| App | `IFeedbackService`, `IAppThemeService`, `IElevationService`, `ISingleInstanceService`, `IAppLifetimeService`, `IClipboardService` | toasts, theme and accent, elevation, single instance, shutdown, clipboard |

The full command table (ViewModel command, service method, data, view) is in `docs/specs/04-architect.md` section 4 (private, not committed).

## Conventions

- Strict MVVM: Views bind to ViewModels, ViewModels call service interfaces, Services never touch WPF (three documented exceptions: theme, clipboard, shutdown).
- No hardcoded visual value: every color, size and duration is a resource key from `Themes/Tokens.xaml`, consumed by `Themes/Styles.xaml`.
- Business errors surface as toasts through `IFeedbackService`, never `MessageBox`.
- UI thread is never blocked: async commands, `CancellationToken` on long operations.
- External processes run with `UseShellExecute = false` and an `ArgumentList`. The single exception is the administrator relaunch (`runas`).
- Application data lives under `%APPDATA%\WindowsCompanion`, never beside the executable.
- A `.claude/settings.json` ships with deny guards on secrets and build output, and a `Stop` hook that runs `dotnet format --verify-no-changes`. Tune or remove the hook to your taste.

## Prerequisites

- .NET SDK 10.0 or later to build. Nothing to install to run the self-contained publish.
- Windows 11 with `winget` (App Installer) for the Updates page. Without it the page shows a message and the rest of the application works.
- Internet access to `api.github.com` is optional. It is used only by the new-version check (one `GET` of the latest release, a generic `User-Agent`, nothing else sent). Without it the check is skipped silently at startup.
- Administrator rights are optional. They are needed to enable a disabled network adapter and flush the DNS cache; the application offers to relaunch itself elevated.

## Installation

```
dotnet restore
dotnet run --project src/WindowsCompanion.Wpf      # development
dotnet build                                       # compilation (warnings are errors)
dotnet format --verify-no-changes                  # format + analyzers
dotnet list package --vulnerable --include-transitive   # dependency vulnerability check, after restore
dotnet publish src/WindowsCompanion.Wpf -c Release -p:PublishProfile=win-x64   # close the application first
```

The publish output is a single self-contained executable in `publish\win-x64\`. It is unsigned: Windows SmartScreen shows a warning the first time it runs from a download.

Set `WINDOWSCOMPANION_DEBUG=1` to log at `Debug` level.

## Tests

```
dotnet test
```

## Palette

System accent: the application follows the accent color chosen in Windows (`ApplySystemAccent`), with `#0067C0` as fallback (`AppConfig.AccentColor`). No other override: backgrounds, text, borders and semantic colors stay Fluent.

## License

Released under the [MIT License](LICENSE).
