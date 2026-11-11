using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WindowsCompanion.Models;
using WindowsCompanion.Services;

namespace WindowsCompanion.ViewModels;

/// <summary>Toast stack: four at most, eviction that spares errors, repeated messages merged (cahier des toasts §3, §6).</summary>
public sealed class ToastHostViewModel : ObservableObject
{
    private readonly TimeProvider _time;
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;

    /// <summary>Initializes a new instance of the <see cref="ToastHostViewModel"/> class.</summary>
    /// <param name="feedback">Source of the toast requests.</param>
    /// <param name="time">Clock.</param>
    public ToastHostViewModel(IFeedbackService feedback, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(feedback);
        _time = time;
        feedback.ToastRequested += OnToastRequested;
    }

    /// <summary>Gets the toasts, oldest first (shown top to bottom).</summary>
    public ObservableCollection<ToastViewModel> Toasts { get; } = [];

    /// <summary>Gets the pending removal of the last closed toast, for the tests.</summary>
    internal Task LastRemoval { get; private set; } = Task.CompletedTask;

    /// <summary>Shows a toast.</summary>
    /// <param name="request">What to show.</param>
    public void Show(ToastRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = _time.GetUtcNow();
        var repeated = Toasts.FirstOrDefault(t => !t.IsClosing
            && t.Title == request.Title
            && t.Message == request.Message
            && now - t.ShownAt < TimeSpan.FromMilliseconds(AppConfig.ToastDuplicateWindowMs));
        if (repeated is not null)
        {
            repeated.Restart(now);
            return;
        }

        Toasts.Add(new ToastViewModel(request, _time, Close));
        while (Toasts.Count(t => !t.IsClosing) > AppConfig.ToastMaxVisible)
        {
            var open = Toasts.Where(t => !t.IsClosing).ToList();
            var evicted = open.FirstOrDefault(t => t.Type != FeedbackType.Danger) ?? open[0];
            Remove(evicted); // no exit animation on eviction (TS-46)
        }
    }

    private void OnToastRequested(object? sender, ToastRequest request)
    {
        // Toasts are created on the UI thread (TS-53): a request from another thread is posted to it.
        if (_context is null || SynchronizationContext.Current == _context)
        {
            Show(request);
        }
        else
        {
            _context.Post(_ => Show(request), null);
        }
    }

    private void Close(ToastViewModel toast)
    {
        if (toast.IsClosing || !Toasts.Contains(toast))
        {
            return;
        }

        toast.IsClosing = true;
        LastRemoval = RemoveAfterExitAsync(toast);
    }

    // The exit animation plays, then the toast leaves the list (cahier des toasts 5.6).
    private async Task RemoveAfterExitAsync(ToastViewModel toast)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(AppConfig.ToastExitDelayMs), _time);
        Remove(toast);
    }

    private void Remove(ToastViewModel toast)
    {
        Toasts.Remove(toast);
        toast.Dispose();
    }
}
