using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using WindowsCompanion.Models;
using WindowsCompanion.Resources;
using WindowsCompanion.Services;
using WindowsCompanion.Services.Data;
using WindowsCompanion.Services.Platform;
using WindowsCompanion.ViewModels;
using WindowsCompanion.Views;
using WindowsCompanion.Views.Layout;
using Wpf.Ui;
using Wpf.Ui.Abstractions;

namespace WindowsCompanion;

/// <summary>Composition root: logging, global handlers, single instance, host, theme, shell, background startup readings.</summary>
public partial class App
{
    private const string LogOutputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppConfig.AppName,
        "logs",
        "app-.log");

    private IHost? _host;
    private WindowPlacement? _closingPlacement;

    private async void OnStartup(object sender, StartupEventArgs e)
    {
        // 1. Bootstrap logger: a host build failure is still recorded.
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(LogPath, rollingInterval: RollingInterval.Day, outputTemplate: LogOutputTemplate, formatProvider: CultureInfo.InvariantCulture)
            .CreateBootstrapLogger();

        // 2. The three global handlers (rules/errors.md).
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            // 3. Host: Serilog, DI.
            _host = BuildHost(e.Args);
            var services = _host.Services;

            // 4. Single instance: a second launch activates the first one and leaves (F-FEN-07).
            var singleInstance = services.GetRequiredService<ISingleInstanceService>();
            if (!singleInstance.TryAcquire(e.Args))
            {
                singleInstance.SignalFirstInstance();
                Log.Information("Une instance tourne deja, activation demandee");
                Shutdown();
                return;
            }

            await _host.StartAsync().ConfigureAwait(true);
            ApplyCulture();

            // 5. Settings, then accent and theme before any window.
            var preferences = services.GetRequiredService<IPreferencesService>();
            var settingsReadable = await preferences.LoadAsync(CancellationToken.None).ConfigureAwait(true);
            services.GetRequiredService<IAppThemeService>().Initialize(preferences.Current.Theme);

            // 6. Shell: geometry restored, shown once, complete (rules/splash.md, no hidden window).
            var shell = services.GetRequiredService<MainWindow>();
            RestorePlacement(shell, preferences.Current.Window);
            shell.Closing += OnShellClosing;
            shell.Closed += OnShellClosed;
            MainWindow = shell;
            shell.Show();
            shell.Activate();
            Log.Information("Windows Companion {Version} demarre", AppConfig.AppVersion);

            // Background source: the activation request arrives on a thread-pool thread.
            singleInstance.ListenForActivation(() => Dispatcher.InvokeAsync(ActivateShell));

            // 7. Startup readings after the window is shown (T-11).
            await services.GetRequiredService<ShellViewModel>()
                .InitializeAsync(PageKeys.FromArguments(e.Args), !settingsReadable, CancellationToken.None)
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            // Startup boundary: whatever failed, record it and leave instead of keeping a process with no window.
            Log.Fatal(ex, "Echec du demarrage");
            Shutdown(1);
        }
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        _host?.Dispose();
        Log.CloseAndFlush();
    }

    private static IHost BuildHost(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseSerilog((_, configuration) =>
            {
                configuration
                    .MinimumLevel.Is(DebugEnabled() ? LogEventLevel.Debug : Enum.Parse<LogEventLevel>(AppConfig.LogLevel))
                    .WriteTo.File(
                        path: LogPath,
                        rollingInterval: RollingInterval.Day,
                        fileSizeLimitBytes: AppConfig.LogMaxBytes,
                        rollOnFileSizeLimit: true,
                        retainedFileCountLimit: AppConfig.LogRetainedFileCount,
                        outputTemplate: LogOutputTemplate,
                        formatProvider: CultureInfo.InvariantCulture);

                if (DebugEnabled())
                {
                    configuration.WriteTo.Debug(formatProvider: CultureInfo.InvariantCulture);
                }
            })
            .ConfigureServices(Register)
            .Build();

    private static void Register(IServiceCollection services)
    {
        // Common
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
        services.AddSingleton(_ => new HttpClient());

        // WPF UI infrastructure, used as shipped (ISnackbarService replaced by the toast host, D-01)
        services.AddSingleton<IContentDialogService, ContentDialogService>();
        services.AddSingleton<INavigationViewPageProvider, NavigationPageProvider>();
        services.AddSingleton<PageNavigator>();
        services.AddSingleton<IPageNavigator>(sp => sp.GetRequiredService<PageNavigator>());

        // Data
        services.AddSingleton<IAppPaths, AppPaths>();
        services.AddSingleton<IJsonFileStore, JsonFileStore>();
        services.AddSingleton<IPreferencesService, PreferencesService>();
        services.AddSingleton<IHistoryService, HistoryService>();

        // Application services
        services.AddSingleton<IFeedbackService, FeedbackService>();
        services.AddSingleton<IAppThemeService, AppThemeService>();
        services.AddSingleton<IElevationService, ElevationService>();
        services.AddSingleton<ISingleInstanceService, SingleInstanceService>();
        services.AddSingleton<IAppLifetimeService, AppLifetimeService>();
        services.AddSingleton<IClipboardService, ClipboardService>();

        // Platform and business services
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<ICimClient, CimClient>();
        services.AddSingleton<IRegistryReader, RegistryReader>();
        services.AddSingleton<INativeSystemInfo, NativeSystemInfo>();
        services.AddSingleton<IDxgiAdapterReader, DxgiAdapterReader>();
        services.AddSingleton<IWingetService, WingetService>();
        services.AddSingleton<IRunningAppService, RunningAppService>();
        services.AddSingleton<IHardwareService, HardwareService>();
        services.AddSingleton<ISystemInfoService, SystemInfoService>();
        services.AddSingleton<INetworkService, NetworkService>();
        services.AddSingleton<IExportService, ExportService>();
        services.AddSingleton<IRecommendationService, RecommendationService>();

        // Shell and toasts
        services.AddSingleton<MainWindow>();
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<ToastHostViewModel>();

        // Page ViewModels: singletons, the installation queue survives navigation (D-03)
        services.AddSingleton<OverviewViewModel>();
        services.AddSingleton<UpdatesViewModel>();
        services.AddSingleton<HardwareViewModel>();
        services.AddSingleton<SystemViewModel>();
        services.AddSingleton<NetworkViewModel>();
        services.AddSingleton<HistoryViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<IPageViewModel>(sp => sp.GetRequiredService<OverviewViewModel>());
        services.AddSingleton<IPageViewModel>(sp => sp.GetRequiredService<UpdatesViewModel>());
        services.AddSingleton<IPageViewModel>(sp => sp.GetRequiredService<HardwareViewModel>());
        services.AddSingleton<IPageViewModel>(sp => sp.GetRequiredService<SystemViewModel>());
        services.AddSingleton<IPageViewModel>(sp => sp.GetRequiredService<NetworkViewModel>());
        services.AddSingleton<IPageViewModel>(sp => sp.GetRequiredService<HistoryViewModel>());
        services.AddSingleton<IPageViewModel>(sp => sp.GetRequiredService<SettingsViewModel>());

        // Pages
        services.AddTransient<OverviewView>();
        services.AddTransient<UpdatesView>();
        services.AddTransient<HardwareView>();
        services.AddTransient<SystemView>();
        services.AddTransient<NetworkView>();
        services.AddTransient<HistoryView>();
        services.AddTransient<SettingsView>();
    }

    // App "WindowsCompanion" -> env var WINDOWSCOMPANION_DEBUG=1.
    private static bool DebugEnabled()
    {
        var key = new string(AppConfig.AppName.Select(c => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '_').ToArray()) + "_DEBUG";
        return Environment.GetEnvironmentVariable(key) == "1";
    }

    private static void ApplyCulture()
    {
        var culture = CultureInfo.GetCultureInfo(AppConfig.DefaultCulture);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        // WPF formats bindings with en-US unless told otherwise.
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));
    }

    // A saved position is restored only if it still lies on a connected screen (F-FEN-08), else the window is centered.
    private static void RestorePlacement(Window shell, WindowPlacement? placement)
    {
        if (placement is null)
        {
            return;
        }

        shell.Width = Math.Max(placement.Width, AppConfig.WindowMinWidth);
        shell.Height = Math.Max(placement.Height, AppConfig.WindowMinHeight);
        if (placement.Left >= SystemParameters.VirtualScreenLeft
            && placement.Top >= SystemParameters.VirtualScreenTop
            && placement.Left + shell.Width <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth
            && placement.Top + shell.Height <= SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight)
        {
            shell.WindowStartupLocation = WindowStartupLocation.Manual;
            shell.Left = placement.Left;
            shell.Top = placement.Top;
        }

        if (placement.Maximized)
        {
            shell.WindowState = WindowState.Maximized;
        }
    }

    private void ActivateShell()
    {
        if (MainWindow is not { } shell)
        {
            return;
        }

        if (shell.WindowState == WindowState.Minimized)
        {
            shell.WindowState = WindowState.Normal;
        }

        shell.Activate();
    }

    // Captured while the window still has its bounds; skipped when the close was cancelled (installation in progress).
    private void OnShellClosing(object? sender, CancelEventArgs e)
    {
        if (e.Cancel || sender is not Window shell)
        {
            return;
        }

        var bounds = shell.WindowState == WindowState.Normal ? new Rect(shell.Left, shell.Top, shell.Width, shell.Height) : shell.RestoreBounds;
        _closingPlacement = new WindowPlacement(bounds.Left, bounds.Top, bounds.Width, bounds.Height, shell.WindowState == WindowState.Maximized);
    }

    private async void OnShellClosed(object? sender, EventArgs e)
    {
        try
        {
            if (_host is not null)
            {
                var placement = _closingPlacement;
                await _host.Services.GetRequiredService<IPreferencesService>()
                    .UpdateAsync(s => s with { Window = placement ?? s.Window }, CancellationToken.None)
                    .ConfigureAwait(true);
                await _host.StopAsync().ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            // Shutdown boundary: the process is leaving either way.
            Log.Error(ex, "Echec de l'enregistrement de la fenetre a la fermeture");
        }
        finally
        {
            Shutdown();
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Exception non geree sur le thread UI");
        if (MainWindow is { IsLoaded: true })
        {
            _host?.Services.GetService<IFeedbackService>()?.Show(
                new ResultError(FeedbackType.Danger, Strings.Toast_OperationFailed_Title, e.Exception.Message));
        }

        e.Handled = true;
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.ExceptionObject as Exception, "Exception non geree sur un thread d'arriere-plan");
        Log.CloseAndFlush();
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Tache en echec non observee");
        e.SetObserved();
    }
}
