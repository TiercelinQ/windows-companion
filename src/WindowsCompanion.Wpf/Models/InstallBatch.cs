namespace WindowsCompanion.Models;

/// <summary>
/// Counting rules of an installation batch (F-MAJ-07, F-MAJ-08): k of N, and the global percentage.
/// A batch starts when items are queued while nothing runs, and grows when items are added during it.
/// </summary>
public sealed class InstallBatch
{
    // Share of an item's slot reached at the end of its download (F-MAJ-07).
    private const double DownloadShare = 70;

    /// <summary>Gets the number of items in the batch (N).</summary>
    public int Total { get; private set; }

    /// <summary>Gets the number of items already processed, whatever their result.</summary>
    public int Completed { get; private set; }

    /// <summary>Gets the 1-based position of the item in progress (k).</summary>
    public int CurrentPosition => Math.Min(Completed + 1, Total);

    /// <summary>Adds queued items to the batch.</summary>
    /// <param name="count">Number of items added.</param>
    public void Add(int count) => Total += count;

    /// <summary>Removes a waiting item from the batch.</summary>
    public void RemoveWaiting() => Total = Math.Max(Completed, Total - 1);

    /// <summary>Records the end of the item in progress.</summary>
    public void MarkCompleted() => Completed = Math.Min(Completed + 1, Total);

    /// <summary>Clears the batch once the queue is empty.</summary>
    public void Reset()
    {
        Total = 0;
        Completed = 0;
    }

    /// <summary>Computes the global percentage of the batch.</summary>
    /// <param name="current">Progress of the item in progress, null between two items.</param>
    /// <returns>A percentage between 0 and 100.</returns>
    public double GlobalPercent(InstallProgress? current)
    {
        if (Total == 0)
        {
            return 0;
        }

        // winget reports no installer progress: the slot stays at 70 % during installation (cahier E5).
        var fraction = current switch
        {
            null => 0,
            { Phase: InstallPhase.Downloading } => DownloadShare * Math.Clamp(current.Percent, 0, 100) / 100,
            _ => DownloadShare,
        };

        return Math.Clamp((Completed + (fraction / 100)) / Total * 100, 0, 100);
    }
}
