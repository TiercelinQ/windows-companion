using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using WindowsCompanion.Helpers;
using WindowsCompanion.Models;
using WindowsCompanion.Resources;
using WindowsCompanion.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace WindowsCompanion.ViewModels;

/// <summary>Shell: navigation pane, status bar, relaunch as administrator, close confirmation, check for a newer version.</summary>
public sealed partial class ShellViewModel : ObservableObject, IRecipient<ElevationRequestedMessage>, IRecipient<AppUpdateCheckRequestedMessage>
{
    private readonly IReadOnlyList<IPageViewModel> _pages;
    private readonly HardwareViewModel _hardware;
    private readonly SystemViewModel _system;
    private readonly NetworkViewModel _network;
    private readonly HistoryViewModel _history;
    private readonly IPageNavigator _navigator;
    private readonly IPreferencesService _preferences;
    private readonly IElevationService _elevation;
    private readonly IUpdateCheckService _updateCheck;
    private readonly IAppLifetimeService _lifetime;
    private readonly IContentDialogService _dialogs;
    private readonly IFeedbackService _feedback;
    private readonly ILogger<ShellViewModel> _logger;
    private bool _narrow;

    [ObservableProperty]
    private bool _isPaneOpen = true;

    [ObservableProperty]
    private PageKey _currentPage;

    [ObservableProperty]
    private int _contentColumns = 2;

    [ObservableProperty]
    private int _tileColumns = 4;

    /// <summary>Initializes a new instance of the <see cref="ShellViewModel"/> class.</summary>
    /// <param name="updates">Updates page, also the source of the status bar and of the badge.</param>
    /// <param name="pages">Every page ViewModel, for F5 and the entry hook.</param>
    /// <param name="hardware">Hardware page, analyzed at startup.</param>
    /// <param name="system">System page, read at startup.</param>
    /// <param name="network">Network page, read at startup.</param>
    /// <param name="history">History page, read at startup.</param>
    /// <param name="navigator">Page navigation.</param>
    /// <param name="preferences">Settings.</param>
    /// <param name="elevation">Elevation state and relaunch.</param>
    /// <param name="updateCheck">Looks for a newer version of the application (D-13).</param>
    /// <param name="lifetime">Closes the application.</param>
    /// <param name="dialogs">WPF UI confirmation dialogs.</param>
    /// <param name="feedback">Toasts.</param>
    /// <param name="messenger">Shared messages.</param>
    /// <param name="logger">Logger.</param>
    public ShellViewModel(
        UpdatesViewModel updates,
        IEnumerable<IPageViewModel> pages,
        HardwareViewModel hardware,
        SystemViewModel system,
        NetworkViewModel network,
        HistoryViewModel history,
        IPageNavigator navigator,
        IPreferencesService preferences,
        IElevationService elevation,
        IUpdateCheckService updateCheck,
        IAppLifetimeService lifetime,
        IContentDialogService dialogs,
        IFeedbackService feedback,
        IMessenger messenger,
        ILogger<ShellViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(navigator);
        ArgumentNullException.ThrowIfNull(messenger);
        Updates = updates;
        _pages = [.. pages];
        _hardware = hardware;
        _system = system;
        _network = network;
        _history = history;
        _navigator = navigator;
        _preferences = preferences;
        _elevation = elevation;
        _updateCheck = updateCheck;
        _lifetime = lifetime;
        _dialogs = dialogs;
        _feedback = feedback;
        _logger = logger;

        navigator.Navigated += OnNavigated;
        messenger.RegisterAll(this);
    }

    /// <summary>Gets the Updates page ViewModel: status bar and navigation badge bind to it (A7).</summary>
    public UpdatesViewModel Updates { get; }

    /// <summary>Gets a value indicating whether the process runs as administrator.</summary>
    public bool IsElevated => _elevation.IsElevated;

    /// <summary>Gets the rights text of the status bar (F-FEN-12).</summary>
    public string RightsText => IsElevated ? Strings.Status_RightsAdmin : Strings.Status_RightsStandard;

    /// <summary>Gets a value indicating whether the window may close without asking.</summary>
    public bool ExitConfirmed { get; private set; }

    /// <summary>Gets a value indicating whether closing the window needs a confirmation (F-LIM-04).</summary>
    public bool NeedsCloseConfirmation => !ExitConfirmed && Updates.IsInstalling;

    /// <summary>Opens the start page, then reads winget, hardware, system, network and history in the background.</summary>
    /// <param name="startPage">Page requested by <c>--page={id}</c>.</param>
    /// <param name="settingsWereReset">Whether the settings file was unreadable (N-30).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task completed once every startup reading has ended.</returns>
    public async Task InitializeAsync(PageKey startPage, bool settingsWereReset, CancellationToken ct)
    {
        IsPaneOpen = !_preferences.Current.NavigationCollapsed;
        _navigator.Navigate(startPage);
        if (settingsWereReset)
        {
            _feedback.Show(new(FeedbackType.Warning, Strings.Toast_Reset_Title, Strings.Toast_Reset_Message));
        }

        await Task.WhenAll(
            Updates.InitializeAsync(ct),
            _hardware.InitializeAsync(ct),
            _system.InitializeAsync(ct),
            _network.InitializeAsync(ct),
            _history.InitializeAsync(ct),
            CheckAppUpdateAsync(manual: false, ct));
    }

    /// <summary>Folds the pane at 900 or less, restores the saved state above (F-NAV-07, A6).</summary>
    /// <param name="width">Window width.</param>
    public void OnWindowWidthChanged(double width)
    {
        // 1100 or less: cards on one column, tiles on two (cahier 3.3).
        var wide = width > AppConfig.WideLayoutThreshold;
        ContentColumns = wide ? 2 : 1;
        TileColumns = wide ? 4 : 2;

        var narrow = width <= AppConfig.NavCollapseThreshold;
        if (narrow == _narrow)
        {
            return;
        }

        _narrow = narrow;
        IsPaneOpen = !narrow && !_preferences.Current.NavigationCollapsed;
    }

    /// <summary>Asks whether to interrupt the installation and quit; cancels it when confirmed (F-LIM-04).</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when the window may close.</returns>
    public async Task<bool> ConfirmCloseAsync(CancellationToken ct)
    {
        var result = await _dialogs.ShowSimpleDialogAsync(
            new SimpleContentDialogCreateOptions
            {
                Title = Strings.Dialog_CloseTitle,
                Content = Strings.Dialog_CloseMessage,
                PrimaryButtonText = Strings.Dialog_CloseConfirm,
                CloseButtonText = Strings.Dialog_Continue,
            },
            ct);

        if (result != ContentDialogResult.Primary)
        {
            return false;
        }

        await Updates.CancelAllAndWaitAsync();
        ExitConfirmed = true;
        return true;
    }

    /// <inheritdoc />
    public void Receive(ElevationRequestedMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        RelaunchAsAdminCommand.Execute(message.Page);
    }

    /// <inheritdoc />
    public void Receive(AppUpdateCheckRequestedMessage message) => _ = CheckAppUpdateAsync(manual: true, CancellationToken.None);

    [RelayCommand]
    private void Navigate(PageKey page)
    {
        _navigator.Navigate(page);
        if (_narrow)
        {
            IsPaneOpen = false;
        }
    }

    [RelayCommand]
    private async Task TogglePaneAsync(CancellationToken ct)
    {
        IsPaneOpen = !IsPaneOpen;
        if (!_narrow)
        {
            var collapsed = !IsPaneOpen;
            await _preferences.UpdateAsync(s => s with { NavigationCollapsed = collapsed }, ct);
        }
    }

    [RelayCommand]
    private Task RunPrimaryAsync()
    {
        var command = _pages.FirstOrDefault(p => p.Page == CurrentPage)?.PrimaryCommand;
        return command is not null && command.CanExecute(null) ? command.ExecuteAsync(null) : Task.CompletedTask;
    }

    [RelayCommand]
    private async Task RelaunchAsAdminAsync(PageKey? page)
    {
        if (IsElevated)
        {
            return;
        }

        var installing = Updates.IsInstalling;
        if (installing)
        {
            var answer = await _dialogs.ShowSimpleDialogAsync(
                new SimpleContentDialogCreateOptions
                {
                    Title = Strings.Dialog_RelaunchTitle,
                    Content = Strings.Dialog_RelaunchMessage,
                    PrimaryButtonText = Strings.Dialog_RelaunchConfirm,
                    CloseButtonText = Strings.Dialog_Continue,
                },
                CancellationToken.None);
            if (answer != ContentDialogResult.Primary)
            {
                return;
            }
        }

        var result = _elevation.RelaunchElevated(page ?? CurrentPage);
        if (!result.Ok)
        {
            _feedback.Show(result.Error!);
            return;
        }

        if (installing)
        {
            await Updates.CancelAllAndWaitAsync();
        }

        ExitConfirmed = true;
        _lifetime.CloseMainWindow();
    }

    private void OnNavigated(object? sender, PageKey page)
    {
        CurrentPage = page;
        if (_pages.FirstOrDefault(p => p.Page == page) is { } viewModel)
        {
            _ = EnterPageAsync(viewModel);
        }
    }

    // Automatic at startup: only a newer version is announced. Manual (Settings): every outcome is announced (D-13).
    // Also fire-and-forget from the message handler, so faults are logged here (rules/threading.md).
    private async Task CheckAppUpdateAsync(bool manual, CancellationToken ct)
    {
        try
        {
            var result = await _updateCheck.CheckAsync(manual, ct);
            if (!result.Ok)
            {
                if (manual)
                {
                    _feedback.Show(result.Error!);
                }

                return;
            }

            var check = result.Data!;
            if (check.Status == UpdateCheckStatus.Available)
            {
                _feedback.Show(
                    new(FeedbackType.Info, Strings.Toast_CompanionNew_Title, Formats.Template(Strings.Toast_CompanionNew_Message, check.LatestVersion, AppConfig.AppVersion)),
                    new ToastAction(Strings.Toast_CompanionDownload, true, () =>
                    {
                        _updateCheck.OpenReleasesPage();
                        return Task.CompletedTask;
                    }));
            }
            else if (check.Status == UpdateCheckStatus.UpToDate && manual)
            {
                _feedback.Show(new(FeedbackType.Success, Strings.Toast_CompanionCurrent_Title, Formats.Template(Strings.Toast_CompanionCurrent_Message, AppConfig.AppVersion)));
            }
        }
        catch (OperationCanceledException)
        {
            // not an error - rules/errors.md
        }
        catch (Exception ex)
        {
            LogUpdateCheckFailed(_logger, ex);
        }
    }

    // Fire-and-forget page entry: faults are logged, never lost (rules/threading.md).
    private async Task EnterPageAsync(IPageViewModel viewModel)
    {
        try
        {
            await viewModel.OnNavigatedToAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            LogPageEntryFailed(_logger, viewModel.Page, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Echec de la verification de la version de l'application")]
    private static partial void LogUpdateCheckFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Echec de l'ouverture de la page {Page}")]
    private static partial void LogPageEntryFailed(ILogger logger, PageKey page, Exception exception);
}
