using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using WindowsCompanion.ViewModels;
using WindowsCompanion.Views.Layout;
using Wpf.Ui;
using Wpf.Ui.Abstractions;

namespace WindowsCompanion.Views;

/// <summary>Shell window. Code-behind limited to wiring the WPF UI hosts and view plumbing (size, close).</summary>
public partial class MainWindow
{
    private readonly ShellViewModel _viewModel;
    private readonly ILogger<MainWindow> _logger;

    /// <summary>Initializes a new instance of the <see cref="MainWindow"/> class.</summary>
    /// <param name="viewModel">Shell ViewModel.</param>
    /// <param name="toasts">Toast host ViewModel.</param>
    /// <param name="pages">Page provider of the navigation view.</param>
    /// <param name="navigator">Page navigator bound to the navigation view.</param>
    /// <param name="dialogs">WPF UI dialog service.</param>
    /// <param name="logger">Logger.</param>
    public MainWindow(
        ShellViewModel viewModel,
        ToastHostViewModel toasts,
        INavigationViewPageProvider pages,
        PageNavigator navigator,
        IContentDialogService dialogs,
        ILogger<MainWindow> logger)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(navigator);
        ArgumentNullException.ThrowIfNull(dialogs);

        _viewModel = viewModel;
        _logger = logger;
        DataContext = viewModel;
        InitializeComponent();

        // Title bar and pane item contents do not inherit the DataContext (rules/xaml.md).
        TitleHeader.DataContext = viewModel;
        BrandImage.Source = LoadBrandImage();
        UpdatesNavContent.DataContext = viewModel.Updates;
        ToastHostView.DataContext = toasts;

        NavView.SetPageProviderService(pages);
        navigator.Attach(NavView);
        dialogs.SetDialogHost(DialogHost);

        SizeChanged += (_, e) => _viewModel.OnWindowWidthChanged(e.NewSize.Width);
        Closing += OnClosing;
    }

    // The PNG, never the .ico (the decoder would pick a small frame). Order matters: decode size, then source.
    private BitmapImage LoadBrandImage()
    {
        var size = (double)FindResource("BrandMarkSize");

        var image = new BitmapImage();
        image.BeginInit();
        image.DecodePixelWidth = (int)Math.Ceiling(size * VisualTreeHelper.GetDpi(this).DpiScaleX);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri("pack://application:,,,/Assets/icon.png", UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }

    // An installation in progress: the close is cancelled while the user answers, then replayed (F-LIM-04).
    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_viewModel.NeedsCloseConfirmation)
        {
            return;
        }

        e.Cancel = true;
        try
        {
            if (await _viewModel.ConfirmCloseAsync(CancellationToken.None))
            {
                Close();
            }
        }
        catch (Exception ex)
        {
            LogCloseFailed(_logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Echec de la confirmation de fermeture")]
    private static partial void LogCloseFailed(ILogger logger, Exception exception);
}
