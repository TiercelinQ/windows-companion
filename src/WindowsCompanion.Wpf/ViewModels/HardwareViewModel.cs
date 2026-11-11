using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WindowsCompanion.Helpers;
using WindowsCompanion.Models;
using WindowsCompanion.Resources;
using WindowsCompanion.Services;

namespace WindowsCompanion.ViewModels;

/// <summary>A graphics adapter block.</summary>
/// <param name="Title">Block title.</param>
/// <param name="Rows">Lines.</param>
public sealed record GpuBlock(string Title, IReadOnlyList<InfoRow> Rows);

/// <summary>A volume as shown by the overview storage card and the hardware storage block.</summary>
/// <param name="Info">The volume.</param>
/// <param name="Title">"C: · model" (overview) line.</param>
/// <param name="BlockTitle">"Disque 0 (C:)" (hardware) title.</param>
/// <param name="MediaLabel">Medium chip text.</param>
/// <param name="UsedText">"612 Go utilisés sur 931 Go".</param>
/// <param name="PercentText">"66 % utilisé", with the level suffix.</param>
/// <param name="ShortPercentText">"66 %".</param>
/// <param name="FreeText">"319 Go libres".</param>
/// <param name="FreeLabel">"Espace libre", "Presque plein" or "Plein".</param>
/// <param name="GaugeName">Accessible name of the gauge.</param>
/// <param name="HasVolume">Whether a volume is mounted (a gauge is shown).</param>
/// <param name="Rows">Detail lines of the hardware block.</param>
public sealed record VolumeRow(
    VolumeInfo Info,
    string Title,
    string BlockTitle,
    string MediaLabel,
    string UsedText,
    string PercentText,
    string ShortPercentText,
    string FreeText,
    string FreeLabel,
    string GaugeName,
    bool HasVolume,
    IReadOnlyList<InfoRow> Rows)
{
    /// <summary>Gets the usage level.</summary>
    public UsageLevel Level => Info.Level;

    /// <summary>Gets the used percentage, for the gauge.</summary>
    public double Percent => Info.UsedPercent;

    /// <summary>Builds the display of a volume.</summary>
    /// <param name="info">The volume.</param>
    /// <returns>The row.</returns>
    public static VolumeRow From(VolumeInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        var suffix = info.Level switch
        {
            UsageLevel.Critical => Strings.Usage_SuffixFull,
            UsageLevel.Attention => Strings.Usage_SuffixAlmostFull,
            _ => string.Empty,
        };

        return new VolumeRow(
            info,
            info.DiskModel.Length == 0 ? info.DriveLetter ?? string.Empty : Formats.Template(Strings.Storage_LineTitle, info.DriveLetter ?? string.Empty, info.DiskModel),
            InfoRows.VolumeTitle(info),
            InfoRows.MediaLabel(info.Media),
            Formats.Template(Strings.Storage_Used, Formats.Gb(info.UsedBytes, 0), Formats.Gb(info.CapacityBytes, 0)),
            Formats.Template(Strings.Storage_PercentUsed, Formats.Percent(info.UsedPercent)) + suffix,
            Formats.Percent(info.UsedPercent),
            Formats.Template(Strings.Storage_Free, Formats.Gb(info.FreeBytes, 0)),
            info.Level switch
            {
                UsageLevel.Critical => Strings.Usage_Full,
                UsageLevel.Attention => Strings.Usage_AlmostFull,
                _ => Strings.Usage_FreeSpace,
            },
            Formats.Template(Strings.Storage_GaugeName, info.DriveLetter ?? string.Empty),
            info.DriveLetter is not null,
            InfoRows.Volume(info));
    }
}

/// <summary>Hardware page: snapshot of the last analysis, export, summary (cahier 4.4).</summary>
public sealed partial class HardwareViewModel : ObservableObject, IPageViewModel, IRecipient<SystemSnapshotMessage>
{
    private readonly IHardwareService _hardware;
    private readonly IExportService _export;
    private readonly IClipboardService _clipboard;
    private readonly IFeedbackService _feedback;
    private readonly IMessenger _messenger;
    private readonly TimeProvider _time;
    private SystemSnapshot? _system;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(HasSnapshot), nameof(CpuRows), nameof(MemoryRows), nameof(BoardRows), nameof(Gpus), nameof(Volumes),
        nameof(ComponentsText), nameof(LastAnalysisText), nameof(CpuChip), nameof(MemoryChip), nameof(MemoryUsedText),
        nameof(MemoryAvailableText), nameof(MemoryPercent), nameof(StorageChip), nameof(StorageLevel), nameof(HasStorageChip))]
    private HardwareSnapshot? _snapshot;

    [ObservableProperty]
    private bool _isAnalyzing;

    /// <summary>Initializes a new instance of the <see cref="HardwareViewModel"/> class.</summary>
    /// <param name="hardware">Hardware reading.</param>
    /// <param name="export">Reports and summary.</param>
    /// <param name="clipboard">Clipboard.</param>
    /// <param name="feedback">Toasts.</param>
    /// <param name="messenger">Shared messages.</param>
    /// <param name="time">Clock.</param>
    public HardwareViewModel(IHardwareService hardware, IExportService export, IClipboardService clipboard, IFeedbackService feedback, IMessenger messenger, TimeProvider time)
    {
        _hardware = hardware;
        _export = export;
        _clipboard = clipboard;
        _feedback = feedback;
        _messenger = messenger;
        _time = time;
        messenger.RegisterAll(this);
    }

    /// <inheritdoc />
    public PageKey Page => PageKey.Hardware;

    /// <inheritdoc />
    public IAsyncRelayCommand PrimaryCommand => AnalyzeCommand;

    /// <summary>Gets a value indicating whether a snapshot exists.</summary>
    public bool HasSnapshot => Snapshot is not null;

    /// <summary>Gets the processor lines.</summary>
    public IReadOnlyList<InfoRow> CpuRows => Snapshot is { } s ? InfoRows.Cpu(s.Cpu) : [];

    /// <summary>Gets the memory lines.</summary>
    public IReadOnlyList<InfoRow> MemoryRows => Snapshot is { } s ? InfoRows.Memory(s.Memory) : [];

    /// <summary>Gets the motherboard lines.</summary>
    public IReadOnlyList<InfoRow> BoardRows => Snapshot is { } s ? InfoRows.Board(s.Board) : [];

    /// <summary>Gets one block per physical graphics adapter (F-LIM-08).</summary>
    public IReadOnlyList<GpuBlock> Gpus => Snapshot is { } s
        ? [.. s.Gpus.Select(g => new GpuBlock(s.Gpus.Count > 1 ? g.Model : Strings.Hw_Graphics, InfoRows.Gpu(g, s.Displays)))]
        : [];

    /// <summary>Gets the volumes, by disk then letter (F-MAT-15).</summary>
    public IReadOnlyList<VolumeRow> Volumes => Snapshot is { } s ? [.. s.Volumes.Select(VolumeRow.From)] : [];

    /// <summary>Gets the summary title, "7 composants détectés".</summary>
    public string ComponentsText => Snapshot is { } s ? Formats.Plural(s.ComponentCount, Strings.Hw_ComponentsOne, Strings.Hw_ComponentsMany) : Strings.Hw_Analyzing;

    /// <summary>Gets the time of the last analysis.</summary>
    public string LastAnalysisText => Snapshot is { } s ? Formats.Template(Strings.Hw_LastAnalysis, Formats.Relative(s.TakenAt, _time.GetLocalNow(), capitalize: false)) : string.Empty;

    /// <summary>Gets the processor chip, "Utilisation 23 %".</summary>
    public string? CpuChip => Snapshot?.Cpu.UsagePercent is { } u ? Formats.Template(Strings.Hw_CpuUsage, Formats.Percent(u)) : null;

    /// <summary>Gets the memory chip, "61 % utilisée".</summary>
    public string? MemoryChip => Snapshot is { } s ? Formats.Template(Strings.Hw_MemoryUsage, Formats.Percent(s.Memory.UsagePercent)) : null;

    /// <summary>Gets the memory usage line.</summary>
    public string MemoryUsedText => Snapshot is { } s ? Formats.Template(Strings.Hw_MemoryUsed, Formats.Gb(s.Memory.UsedBytes, 1), Formats.Gb(s.Memory.VisibleBytes, 1)) : string.Empty;

    /// <summary>Gets the available memory line.</summary>
    public string MemoryAvailableText => Snapshot is { } s ? Formats.Template(Strings.Hw_MemoryAvailable, Formats.Gb(s.Memory.AvailableBytes, 1)) : string.Empty;

    /// <summary>Gets the memory usage, for the gauge.</summary>
    public double MemoryPercent => Snapshot?.Memory.UsagePercent ?? 0;

    /// <summary>Gets the storage chip text, when a volume reaches 80 % (F-MAT-08).</summary>
    public string? StorageChip
    {
        get
        {
            var full = Snapshot?.Volumes.Where(v => v.DriveLetter is not null && v.Level != UsageLevel.Normal).ToList() ?? [];
            return full.Count switch
            {
                0 => null,
                1 => Formats.Template(full[0].Level == UsageLevel.Critical ? Strings.Hw_StorageChipFull : Strings.Hw_StorageChipAlmostFull, full[0].DriveLetter!),
                _ => Formats.Template(Strings.Hw_StorageChipMany, full.Count),
            };
        }
    }

    /// <summary>Gets the level of the storage chip.</summary>
    public UsageLevel StorageLevel => Snapshot?.Volumes.Where(v => v.DriveLetter is not null).Select(v => v.Level).DefaultIfEmpty(UsageLevel.Normal).Max() ?? UsageLevel.Normal;

    /// <summary>Gets a value indicating whether the storage chip is shown.</summary>
    public bool HasStorageChip => StorageChip is not null;

    /// <summary>Runs the startup analysis, without toast (F-MAT-04).</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task completed once the snapshot is published.</returns>
    public Task InitializeAsync(CancellationToken ct) => RunAnalysisAsync(showToast: false, ct);

    /// <inheritdoc />
    public Task OnNavigatedToAsync(CancellationToken ct)
    {
        OnPropertyChanged(nameof(LastAnalysisText));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Receive(SystemSnapshotMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        _system = message.Snapshot;
    }

    [RelayCommand]
    private Task AnalyzeAsync(CancellationToken ct) => RunAnalysisAsync(showToast: true, ct);

    [RelayCommand]
    private async Task ExportAsync(CancellationToken ct)
    {
        var result = await _export.ExportReportAsync(ExportKind.Hardware, new ReportContent(Snapshot, null, null, null), ct);
        PageFeedback.ShowExport(_feedback, _export, result, () => ExportCommand.ExecuteAsync(null));
    }

    [RelayCommand]
    private void CopySummary() => PageFeedback.ShowCopied(_feedback, _clipboard, _export.BuildSummary(Snapshot, _system));

    private async Task RunAnalysisAsync(bool showToast, CancellationToken ct)
    {
        IsAnalyzing = true;
        try
        {
            var snapshot = await _hardware.GetSnapshotAsync(ct);
            Snapshot = snapshot;
            _messenger.Send(new HardwareSnapshotMessage(snapshot));
            if (showToast)
            {
                _feedback.Show(new(FeedbackType.Success, Strings.Toast_Analyzed_Title, Formats.Template(
                    Strings.Toast_Analyzed_Message,
                    snapshot.ComponentCount,
                    Formats.Plural(snapshot.Gpus.Count, Strings.Toast_Analyzed_GpuOne, Strings.Toast_Analyzed_GpuMany),
                    Formats.Plural(snapshot.PhysicalDiskCount, Strings.Summary_DiskOne, Strings.Summary_DiskMany))));
            }
        }
        catch (OperationCanceledException)
        {
            // Not an error.
        }
        finally
        {
            IsAnalyzing = false;
        }
    }
}
