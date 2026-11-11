using WindowsCompanion.Models;

namespace WindowsCompanion.Services;

/// <summary>The only feedback surface of the ViewModels: shows a toast (D-01, cahier des toasts).</summary>
public interface IFeedbackService
{
    /// <summary>Raised for every toast to show; handled by the toast host.</summary>
    event EventHandler<ToastRequest>? ToastRequested;

    /// <summary>Shows a toast.</summary>
    /// <param name="feedback">Severity, title (<see cref="ResultError.Message"/>) and message (<see cref="ResultError.Description"/>).</param>
    /// <param name="actions">Zero to two actions; the first primary one is shown in accent.</param>
    void Show(ResultError feedback, params ToastAction[] actions);
}

/// <summary>Default implementation of <see cref="IFeedbackService"/>.</summary>
public sealed class FeedbackService : IFeedbackService
{
    /// <inheritdoc />
    public event EventHandler<ToastRequest>? ToastRequested;

    /// <inheritdoc />
    public void Show(ResultError feedback, params ToastAction[] actions)
    {
        ArgumentNullException.ThrowIfNull(feedback);
        ArgumentNullException.ThrowIfNull(actions);
        ToastRequested?.Invoke(this, new ToastRequest(feedback.Type, feedback.Message, feedback.Description ?? string.Empty, [.. actions.Take(2)]));
    }
}
