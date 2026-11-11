using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WindowsCompanion.Helpers;
using WindowsCompanion.Models;
using WindowsCompanion.Resources;
using WindowsCompanion.Services;

namespace WindowsCompanion.ViewModels;

/// <summary>An ignored application as shown in Settings.</summary>
/// <param name="Entry">The entry.</param>
/// <param name="Detail">"identifier · reason" line.</param>
public sealed record ExclusionRow(ExclusionEntry Entry, string Detail);

/// <summary>Settings page: sources, installation, ignored applications, appearance (cahier 4.8, v1.0 scope).</summary>
public sealed partial class SettingsViewModel : ObservableObject, IPageViewModel, IRecipient<SettingsChangedMessage>
{
    private readonly IPreferencesService _preferences;
    private readonly IAppThemeService _theme;
    private readonly IFeedbackService _feedback;
    private readonly IMessenger _messenger;
    private bool _loading;

    [ObservableProperty]
    private bool _useWinget;

    [ObservableProperty]
    private bool _useMsStore;

    [ObservableProperty]
    private bool _includeUnknownVersions;

    [ObservableProperty]
    private bool _silentInstall;

    [ObservableProperty]
    private ThemeMode _themeMode;

    [ObservableProperty]
    private bool _hasExclusions;

    /// <summary>Initializes a new instance of the <see cref="SettingsViewModel"/> class.</summary>
    /// <param name="preferences">Settings storage.</param>
    /// <param name="theme">Theme mode.</param>
    /// <param name="feedback">Toasts.</param>
    /// <param name="messenger">Shared messages.</param>
    public SettingsViewModel(IPreferencesService preferences, IAppThemeService theme, IFeedbackService feedback, IMessenger messenger)
    {
        ArgumentNullException.ThrowIfNull(theme);
        _preferences = preferences;
        _theme = theme;
        _feedback = feedback;
        _messenger = messenger;
        theme.ThemeChanged += (_, _) => Load();
        messenger.RegisterAll(this);
        Load();
    }

    /// <inheritdoc />
    public PageKey Page => PageKey.Settings;

    /// <inheritdoc />
    public IAsyncRelayCommand? PrimaryCommand => null;

    /// <summary>Gets the ignored applications.</summary>
    public ObservableCollection<ExclusionRow> Exclusions { get; } = [];

    /// <inheritdoc />
    public Task OnNavigatedToAsync(CancellationToken ct)
    {
        Load();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Receive(SettingsChangedMessage message) => Load();

    partial void OnUseWingetChanged(bool value) => Persist(s => s with { UseWinget = value });

    partial void OnUseMsStoreChanged(bool value) => Persist(s => s with { UseMsStore = value });

    partial void OnIncludeUnknownVersionsChanged(bool value) => Persist(s => s with { IncludeUnknownVersions = value });

    partial void OnSilentInstallChanged(bool value) => Persist(s => s with { SilentInstall = value });

    partial void OnThemeModeChanged(ThemeMode value)
    {
        if (!_loading)
        {
            _ = _theme.SetModeAsync(value, CancellationToken.None);
        }
    }

    [RelayCommand]
    private async Task ReactivateAsync(ExclusionRow? row)
    {
        if (row is null)
        {
            return;
        }

        await _preferences.RemoveExclusionsAsync([row.Entry.Id], CancellationToken.None);
        _feedback.Show(new(FeedbackType.Info, Formats.Template(Strings.Toast_Reactivated_Title, row.Entry.Name), Strings.Toast_Reactivated_Message));
        _messenger.Send(new SettingsChangedMessage());
    }

    // Applied and saved as soon as it changes, no "Save" button (F-PAR-02). The service logs its own I/O failures.
    private void Persist(Func<AppSettings, AppSettings> change)
    {
        if (_loading)
        {
            return;
        }

        _ = PersistAsync(change);
    }

    private async Task PersistAsync(Func<AppSettings, AppSettings> change)
    {
        await _preferences.UpdateAsync(change, CancellationToken.None);
        _messenger.Send(new SettingsChangedMessage());
    }

    private void Load()
    {
        _loading = true;
        try
        {
            var settings = _preferences.Current;
            UseWinget = settings.UseWinget;
            UseMsStore = settings.UseMsStore;
            IncludeUnknownVersions = settings.IncludeUnknownVersions;
            SilentInstall = settings.SilentInstall;
            ThemeMode = _theme.Mode;

            // Rebuilt only when the list changed: another setting leaves the rows (and their controls) in place.
            if (!Exclusions.Select(r => r.Entry).SequenceEqual(settings.Exclusions))
            {
                Exclusions.Clear();
                foreach (var entry in settings.Exclusions)
                {
                    Exclusions.Add(new ExclusionRow(entry, Formats.Template(Strings.Set_ExclusionDetail, entry.Id, entry.Reason)));
                }
            }

            HasExclusions = Exclusions.Count > 0;
        }
        finally
        {
            _loading = false;
        }
    }
}
