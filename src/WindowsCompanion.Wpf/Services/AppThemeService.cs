using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using ThemeMode = WindowsCompanion.Models.ThemeMode;

namespace WindowsCompanion.Services;

/// <summary>Theme mode (System, Light, Dark) and Windows accent (rules/xaml.md, theme switching).</summary>
public interface IAppThemeService
{
    /// <summary>Gets the chosen mode.</summary>
    ThemeMode Mode { get; }

    /// <summary>Raised after the theme or the accent has been applied.</summary>
    event EventHandler? ThemeChanged;

    /// <summary>Applies the startup mode, before any window is created.</summary>
    /// <param name="mode">Mode read from the settings.</param>
    void Initialize(ThemeMode mode);

    /// <summary>Applies and persists a mode.</summary>
    /// <param name="mode">New mode.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task completed once the setting is written.</returns>
    Task SetModeAsync(ThemeMode mode, CancellationToken ct);
}

/// <summary>WPF UI implementation of <see cref="IAppThemeService"/>; bound to WPF by nature, like the theme managers it drives.</summary>
public sealed class AppThemeService : IAppThemeService, IDisposable
{
    private readonly IPreferencesService _preferences;

    /// <summary>Initializes a new instance of the <see cref="AppThemeService"/> class and follows Windows changes.</summary>
    /// <param name="preferences">Stores the chosen mode.</param>
    public AppThemeService(IPreferencesService preferences)
    {
        _preferences = preferences;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    /// <inheritdoc />
    public event EventHandler? ThemeChanged;

    /// <inheritdoc />
    public ThemeMode Mode { get; private set; } = ThemeMode.System;

    /// <inheritdoc />
    public void Initialize(ThemeMode mode)
    {
        Mode = mode;
        Apply();
    }

    /// <inheritdoc />
    public async Task SetModeAsync(ThemeMode mode, CancellationToken ct)
    {
        if (mode != Mode)
        {
            Mode = mode;
            Apply();
        }

        await _preferences.UpdateAsync(s => s with { Theme = mode }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose() => SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;

    private static ApplicationTheme Effective(ThemeMode mode) => mode switch
    {
        ThemeMode.Light => ApplicationTheme.Light,
        ThemeMode.Dark => ApplicationTheme.Dark,
        _ => ApplicationThemeManager.GetSystemTheme() is SystemTheme.Dark or SystemTheme.HCBlack
            ? ApplicationTheme.Dark
            : ApplicationTheme.Light,
    };

    // The Windows accent; the project fallback when it cannot be read (01-scoping.md, palette).
    private static Color Accent()
    {
        try
        {
            return ApplicationAccentColorManager.GetColorizationColor();
        }
        catch (InvalidOperationException)
        {
            return AppConfig.AccentColorValue;
        }
    }

    private void Apply()
    {
        var theme = Effective(Mode);

        // Accent first: the Fluent dictionary captures the accent when it loads (rules/xaml.md).
        ApplicationAccentColorManager.Apply(Accent(), theme);
        ApplicationThemeManager.Apply(theme, WindowBackdropType.Mica, updateAccent: false);
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    // Raised by Windows when the app mode or the accent changes (F-FEN-06).
    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle))
        {
            return;
        }

        // Background source: SystemEvents may raise on its own thread; the theme managers touch the UI.
        Application.Current?.Dispatcher.InvokeAsync(Apply);
    }
}
