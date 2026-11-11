using System.IO;
using WindowsCompanion.Models;
using WindowsCompanion.Services.Platform;

namespace WindowsCompanion.Services;

/// <summary>Detects whether an updated application is still running and closes it gently (F-MAJ-23/24, risk R6).</summary>
public interface IRunningAppService
{
    /// <summary>Tells whether a process runs from the installation folder of the package.</summary>
    /// <param name="item">Updated item.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when the application is open.</returns>
    Task<bool> IsRunningAsync(UpdateItem item, CancellationToken ct);

    /// <summary>Asks the running application to close its windows, without forcing it.</summary>
    /// <param name="item">Updated item.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when every process of the application has exited within the wait.</returns>
    Task<bool> CloseAsync(UpdateItem item, CancellationToken ct);
}

/// <summary>Uninstall registry and process path implementation of <see cref="IRunningAppService"/>.</summary>
/// <param name="registry">Reads the uninstall keys.</param>
/// <param name="processes">Lists and closes processes.</param>
public sealed class RunningAppService(IRegistryReader registry, IProcessRunner processes) : IRunningAppService
{
    private const int MinNameLength = 3;

    /// <inheritdoc />
    public Task<bool> IsRunningAsync(UpdateItem item, CancellationToken ct) =>
        Task.Run(() => FindProcesses(item).Count > 0, ct); // walks every process: kept off the UI thread

    /// <inheritdoc />
    public async Task<bool> CloseAsync(UpdateItem item, CancellationToken ct)
    {
        var timeout = TimeSpan.FromMilliseconds(AppConfig.CloseAppWaitMs);
        var closed = true;
        foreach (var process in await Task.Run(() => FindProcesses(item), ct).ConfigureAwait(false))
        {
            closed &= await processes.CloseAsync(process.Id, timeout, ct).ConfigureAwait(false);
        }

        return closed;
    }

    private static bool NameMatches(string? displayName, string name) =>
        displayName is { Length: >= MinNameLength }
        && (displayName.StartsWith(name, StringComparison.OrdinalIgnoreCase) || name.StartsWith(displayName, StringComparison.OrdinalIgnoreCase));

    private List<RunningProcess> FindProcesses(UpdateItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var locations = InstallLocations(item.Name);
        if (locations.Count == 0)
        {
            return [];
        }

        return [.. processes.GetProcesses().Where(p => p.Path is not null && locations.Exists(l => p.Path.StartsWith(l, StringComparison.OrdinalIgnoreCase)))];
    }

    private List<string> InstallLocations(string name)
    {
        var locations = new List<string>();
        foreach (var key in RegistryPaths.Uninstall)
        {
            foreach (var sub in registry.GetSubKeyNames(key.Root, key.Path))
            {
                var path = key.Path + "\\" + sub;
                if (!NameMatches(registry.GetValue(key.Root, path, "DisplayName") as string, name))
                {
                    continue;
                }

                if (registry.GetValue(key.Root, path, "InstallLocation") is string { Length: > 0 } location)
                {
                    locations.Add(Path.TrimEndingDirectorySeparator(location.Trim('"')) + Path.DirectorySeparatorChar);
                }
            }
        }

        return locations;
    }
}
