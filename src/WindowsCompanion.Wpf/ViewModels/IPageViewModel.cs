using CommunityToolkit.Mvvm.Input;
using WindowsCompanion.Helpers;
using WindowsCompanion.Models;
using WindowsCompanion.Resources;
using WindowsCompanion.Services;

namespace WindowsCompanion.ViewModels;

/// <summary>A page ViewModel: its key, its main operation (F5) and its entry hook.</summary>
public interface IPageViewModel
{
    /// <summary>Gets the page this ViewModel drives.</summary>
    PageKey Page { get; }

    /// <summary>Gets the main operation run by F5, or null when the page has none (F-CLA-04).</summary>
    IAsyncRelayCommand? PrimaryCommand { get; }

    /// <summary>Called each time the page is shown.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task completed once the page data is ready.</returns>
    Task OnNavigatedToAsync(CancellationToken ct);
}

/// <summary>Toasts shared by the pages that export or copy (N-18, N-19, N-27).</summary>
internal static class PageFeedback
{
    /// <summary>Shows the result of an export: "Rapport exporté" with "Ouvrir le dossier", or the failure.</summary>
    /// <param name="feedback">Toasts.</param>
    /// <param name="export">Opens the Explorer.</param>
    /// <param name="result">Export result.</param>
    /// <param name="retry">Runs the export again (N-27).</param>
    public static void ShowExport(IFeedbackService feedback, IExportService export, Result<string> result, Func<Task> retry)
    {
        if (!result.Ok)
        {
            feedback.Show(result.Error!, new ToastAction(Strings.Toast_Retry, true, retry));
            return;
        }

        var path = result.Data!;
        feedback.Show(
            new(FeedbackType.Info, Strings.Toast_Exported_Title, Formats.Template(Strings.Toast_Exported_Message, System.IO.Path.GetFileName(path))),
            new ToastAction(Strings.Toast_OpenFolder, true, () =>
            {
                export.RevealInExplorer(path);
                return Task.CompletedTask;
            }));
    }

    /// <summary>Copies the summary and shows "Résumé copié".</summary>
    /// <param name="feedback">Toasts.</param>
    /// <param name="clipboard">Clipboard.</param>
    /// <param name="summary">Summary text.</param>
    public static void ShowCopied(IFeedbackService feedback, IClipboardService clipboard, string summary)
    {
        feedback.Show(clipboard.SetText(summary)
            ? new(FeedbackType.Success, Strings.Toast_Copied_Title, Strings.Toast_Copied_Message)
            : new(FeedbackType.Warning, Strings.Toast_CopyFailed_Title, Strings.Toast_CopyFailed_Message));
    }
}
