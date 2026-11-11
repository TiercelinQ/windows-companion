using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindowsCompanion.Models;
using WindowsCompanion.Resources;

namespace WindowsCompanion.ViewModels;

/// <summary>One toast: kicker, title, message, up to two actions, timer paused on hover (cahier des toasts §6).</summary>
public sealed partial class ToastViewModel : ObservableObject, IDisposable
{
    private readonly Action<ToastViewModel> _close;
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;
    private readonly ITimer? _timer;
    private TimeSpan _elapsed;
    private bool _paused;

    [ObservableProperty]
    private double _remainingFraction = 1;

    [ObservableProperty]
    private bool _isClosing;

    /// <summary>Initializes a new instance of the <see cref="ToastViewModel"/> class.</summary>
    /// <param name="request">What to show.</param>
    /// <param name="time">Clock driving the timer.</param>
    /// <param name="close">Asks the host to close this toast.</param>
    public ToastViewModel(ToastRequest request, TimeProvider time, Action<ToastViewModel> close)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(time);
        Request = request;
        _close = close;
        ShownAt = time.GetUtcNow();
        Duration = request.Type switch
        {
            FeedbackType.Success => TimeSpan.FromMilliseconds(AppConfig.ToastSuccessMs),
            FeedbackType.Info => TimeSpan.FromMilliseconds(AppConfig.ToastInfoMs),
            FeedbackType.Warning => TimeSpan.FromMilliseconds(AppConfig.ToastWarningMs),
            _ => null, // an error stays until closed (TS-40)
        };

        if (Duration is not null)
        {
            var tick = TimeSpan.FromMilliseconds(AppConfig.ToastTickMs);
            _timer = time.CreateTimer(_ => OnTick(tick), null, tick, tick);
        }
    }

    /// <summary>Gets the request.</summary>
    public ToastRequest Request { get; }

    /// <summary>Gets the severity.</summary>
    public FeedbackType Type => Request.Type;

    /// <summary>Gets the title.</summary>
    public string Title => Request.Title;

    /// <summary>Gets the message.</summary>
    public string Message => Request.Message;

    /// <summary>Gets the actions.</summary>
    public IReadOnlyList<ToastAction> Actions => Request.Actions;

    /// <summary>Gets a value indicating whether the action row is shown.</summary>
    public bool HasActions => Request.Actions.Count > 0;

    /// <summary>Gets the kicker, fixed by the type (TS-20).</summary>
    public string Kicker => Type switch
    {
        FeedbackType.Success => Strings.Toast_KickerSuccess,
        FeedbackType.Info => Strings.Toast_KickerInfo,
        FeedbackType.Warning => Strings.Toast_KickerWarning,
        _ => Strings.Toast_KickerError,
    };

    /// <summary>Gets a value indicating whether the toast is announced as an alert (TS-61).</summary>
    public bool IsAlert => Type is FeedbackType.Warning or FeedbackType.Danger;

    /// <summary>Gets the display duration; null for an error.</summary>
    public TimeSpan? Duration { get; }

    /// <summary>Gets a value indicating whether the timer bar is shown (TS-23).</summary>
    public bool HasTimer => Duration is not null;

    /// <summary>Gets the time the toast was shown.</summary>
    public DateTimeOffset ShownAt { get; private set; }

    /// <summary>Stops the timer while the pointer is over the toast (TS-41).</summary>
    public void Pause() => _paused = true;

    /// <summary>Resumes the timer with the remaining time (TS-41).</summary>
    public void Resume() => _paused = false;

    /// <summary>Restarts the timer of a repeated message (TS-49).</summary>
    /// <param name="now">Time of the repeat.</param>
    public void Restart(DateTimeOffset now)
    {
        _elapsed = TimeSpan.Zero;
        ShownAt = now;
        RemainingFraction = 1;
    }

    /// <inheritdoc />
    public void Dispose() => _timer?.Dispose();

    [RelayCommand]
    private void Close() => _close(this);

    // Runs the action, then closes the toast (TS-43).
    [RelayCommand]
    private async Task InvokeActionAsync(ToastAction? action)
    {
        if (action is null || IsClosing)
        {
            return;
        }

        await action.Execute();
        _close(this);
    }

    // Background source: the timer callback runs on the thread pool; the update is posted to the UI context.
    private void OnTick(TimeSpan tick)
    {
        if (_context is null)
        {
            Advance(tick);
        }
        else
        {
            _context.Post(_ => Advance(tick), null);
        }
    }

    private void Advance(TimeSpan tick)
    {
        if (_paused || IsClosing || Duration is not { } duration)
        {
            return;
        }

        _elapsed += tick;
        RemainingFraction = Math.Max(0, 1 - (_elapsed / duration));
        if (_elapsed >= duration)
        {
            _close(this);
        }
    }
}
