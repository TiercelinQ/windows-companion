using CommunityToolkit.Mvvm.ComponentModel;
using WindowsCompanion.Helpers;
using WindowsCompanion.Models;
using WindowsCompanion.Resources;

namespace WindowsCompanion.ViewModels;

/// <summary>A row of the update table: state, progress, selection (cahier 4.3).</summary>
public sealed partial class UpdateItemViewModel : ObservableObject
{
    private readonly Action _selectionChanged;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSelectable), nameof(StatusText), nameof(CanCancel))]
    private UpdateState _state;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private string? _exitCode;

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Initializes a new instance of the <see cref="UpdateItemViewModel"/> class.</summary>
    /// <param name="model">The update.</param>
    /// <param name="selectionChanged">Called when the row is checked or unchecked.</param>
    public UpdateItemViewModel(UpdateItem model, Action selectionChanged)
    {
        ArgumentNullException.ThrowIfNull(model);
        Model = model;
        _selectionChanged = selectionChanged;
        Initials = AppTile.Initials(model.Name);
        TileIndex = AppTile.ColorIndex(model.Id);
    }

    /// <summary>Gets the update.</summary>
    public UpdateItem Model { get; }

    /// <summary>Gets the two letters of the application tile.</summary>
    public string Initials { get; }

    /// <summary>Gets the stable color index of the application tile (F-MAJ-10).</summary>
    public int TileIndex { get; }

    /// <summary>Gets the application name.</summary>
    public string Name => Model.Name;

    /// <summary>Gets the package identifier.</summary>
    public string Id => Model.Id;

    /// <summary>Gets the installed version, or "Inconnue".</summary>
    public string InstalledText => Model.InstalledVersion ?? Strings.Upd_VersionUnknown;

    /// <summary>Gets the available version.</summary>
    public string AvailableVersion => Model.AvailableVersion;

    /// <summary>Gets the source label.</summary>
    public string SourceText => InfoRows.SourceLabel(Model.Source);

    /// <summary>Gets or sets the start of the processing, used for the duration.</summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>Gets a value indicating whether the row can be checked and installed (F-MAJ-16).</summary>
    public bool IsSelectable => State is UpdateState.Available or UpdateState.Failed or UpdateState.Cancelled;

    /// <summary>Gets a value indicating whether the row shows the cancel button.</summary>
    public bool CanCancel => State is UpdateState.Queued or UpdateState.Installing;

    /// <summary>Gets the text of the state cell.</summary>
    public string StatusText => State switch
    {
        UpdateState.Available => Strings.State_Available,
        UpdateState.Queued => Strings.State_Queued,
        UpdateState.Installing => Strings.State_Installing,
        UpdateState.Installed => Strings.State_Installed,
        UpdateState.AppOpen => Strings.State_AppOpen,
        UpdateState.Failed => Formats.Template(Strings.State_Failed, ExitCode ?? string.Empty),
        _ => Strings.State_Cancelled,
    };

    /// <summary>Gets the accessible name of the row check box.</summary>
    public string SelectName => Formats.Template(Strings.Upd_SelectName, Name);

    /// <summary>Gets the accessible name of the row cancel button.</summary>
    public string CancelName => Formats.Template(Strings.Upd_CancelName, Name);

    partial void OnIsSelectedChanged(bool value)
    {
        if (value && !IsSelectable)
        {
            IsSelected = false;
            return;
        }

        _selectionChanged();
    }

    partial void OnStateChanged(UpdateState value)
    {
        if (!IsSelectable && IsSelected)
        {
            IsSelected = false; // queueing unchecks the row (F-MAJ-16)
        }
    }
}
