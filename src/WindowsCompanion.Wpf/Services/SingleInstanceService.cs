using System.Diagnostics;
using System.Globalization;

namespace WindowsCompanion.Services;

/// <summary>Single instance (F-FEN-07, rules/security.md §7): one process writes the data files.</summary>
public interface ISingleInstanceService : IDisposable
{
    /// <summary>Takes the single-instance lock, after waiting for the process named by <c>--replace={pid}</c>.</summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>True when this process is the only instance.</returns>
    bool TryAcquire(IReadOnlyList<string> args);

    /// <summary>Asks the running instance to activate its window.</summary>
    void SignalFirstInstance();

    /// <summary>Starts listening for activation requests from later instances.</summary>
    /// <param name="onActivation">Called on a thread-pool thread for each request.</param>
    void ListenForActivation(Action onActivation);
}

/// <summary>Named mutex and named event implementation of <see cref="ISingleInstanceService"/>.</summary>
public sealed class SingleInstanceService : ISingleInstanceService
{
    private const string ReplacePrefix = "--replace=";
    private readonly EventWaitHandle _activation = new(false, EventResetMode.AutoReset, AppConfig.ActivationEventName);
    private Mutex? _mutex;
    private bool _owned;
    private RegisteredWaitHandle? _registration;

    /// <inheritdoc />
    public bool TryAcquire(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        WaitForReplacedProcess(args);
        _mutex = new Mutex(initiallyOwned: true, AppConfig.SingleInstanceMutexName, out _owned);
        return _owned;
    }

    /// <inheritdoc />
    public void SignalFirstInstance() => _activation.Set();

    /// <inheritdoc />
    public void ListenForActivation(Action onActivation) =>
        _registration = ThreadPool.RegisterWaitForSingleObject(_activation, (_, _) => onActivation(), null, Timeout.Infinite, executeOnlyOnce: false);

    /// <inheritdoc />
    public void Dispose()
    {
        _registration?.Unregister(null);
        if (_owned)
        {
            _mutex?.ReleaseMutex();
        }

        _mutex?.Dispose();
        _activation.Dispose();
    }

    // Relaunch as administrator: the previous instance closes once the new one started (F-DRO-03).
    private static void WaitForReplacedProcess(IReadOnlyList<string> args)
    {
        var arg = args.FirstOrDefault(a => a.StartsWith(ReplacePrefix, StringComparison.Ordinal));
        if (arg is null || !int.TryParse(arg[ReplacePrefix.Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid))
        {
            return;
        }

        try
        {
            using var previous = Process.GetProcessById(pid);
            previous.WaitForExit(AppConfig.ReplaceWaitMs);
        }
        catch (ArgumentException)
        {
            // Already gone: nothing to wait for.
        }
    }
}
