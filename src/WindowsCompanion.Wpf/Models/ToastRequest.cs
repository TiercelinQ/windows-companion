namespace WindowsCompanion.Models;

/// <summary>A button of a toast (at most two per toast, TS-03).</summary>
/// <param name="Label">Label, three words at most.</param>
/// <param name="IsPrimary">Whether it is the primary (accent) action.</param>
/// <param name="Execute">Work run on click; the toast closes afterwards.</param>
public sealed record ToastAction(string Label, bool IsPrimary, Func<Task> Execute);

/// <summary>A toast to show (cahier des toasts, 6.1).</summary>
/// <param name="Type">Severity; fixes the duration, the persistence and the accessibility role.</param>
/// <param name="Title">Title, one line.</param>
/// <param name="Message">One or two sentences.</param>
/// <param name="Actions">Zero to two actions.</param>
public sealed record ToastRequest(FeedbackType Type, string Title, string Message, IReadOnlyList<ToastAction> Actions);
