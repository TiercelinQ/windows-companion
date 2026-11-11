using WindowsCompanion.Helpers;
using WindowsCompanion.Models;
using WindowsCompanion.Resources;

namespace WindowsCompanion.Services;

/// <summary>Rules of the recommended actions of the overview (F-ACC-10 to F-ACC-15).</summary>
public interface IRecommendationService
{
    /// <summary>Computes the actions, in display order: updates, volumes (fullest first), restart, security.</summary>
    /// <param name="updates">Update figures.</param>
    /// <param name="hardware">Last hardware snapshot.</param>
    /// <param name="system">Last system snapshot.</param>
    /// <param name="now">Current time.</param>
    /// <returns>The actions.</returns>
    IReadOnlyList<RecommendedAction> Compute(UpdatesSummary updates, HardwareSnapshot? hardware, SystemSnapshot? system, DateTimeOffset now);
}

/// <summary>Default implementation of <see cref="IRecommendationService"/>.</summary>
public sealed class RecommendationService : IRecommendationService
{
    /// <inheritdoc />
    public IReadOnlyList<RecommendedAction> Compute(UpdatesSummary updates, HardwareSnapshot? hardware, SystemSnapshot? system, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(updates);
        var actions = new List<RecommendedAction>();

        if (updates.HasSearched)
        {
            actions.Add(updates.PendingCount > 0
                ? new RecommendedAction(
                    ActionKind.Updates,
                    ActionSeverity.Warning,
                    Formats.Plural(updates.PendingCount, Strings.Action_UpdatesOne, Strings.Action_UpdatesMany),
                    updates.KnownBytes > 0 && updates.LargestName is not null
                        ? Formats.Template(Strings.Action_UpdatesDetail, Formats.DownloadTotal(updates.KnownBytes, updates.HasUnknownSize), updates.LargestName, Formats.Size(updates.LargestBytes))
                        : Strings.Action_UpdatesSizeUnknown,
                    Strings.Action_See,
                    PageKey.Updates)
                : new RecommendedAction(ActionKind.UpToDate, ActionSeverity.Success, Strings.Action_UpToDate, Strings.Action_UpToDateDetail, Strings.Action_See, PageKey.Updates));
        }

        if (hardware is not null)
        {
            foreach (var volume in hardware.Volumes.Where(v => v.DriveLetter is not null && v.Level != UsageLevel.Normal).OrderByDescending(v => v.UsedPercent))
            {
                actions.Add(new RecommendedAction(
                    ActionKind.VolumeFull,
                    ActionSeverity.Warning,
                    Formats.Template(Strings.Action_VolumeFull, volume.DriveLetter!, Formats.Percent(volume.UsedPercent)),
                    Formats.Template(volume.Level == UsageLevel.Critical ? Strings.Action_VolumeCriticalDetail : Strings.Action_VolumeAttentionDetail, Formats.Gb(volume.FreeBytes, 0)),
                    Strings.Action_Details,
                    PageKey.Hardware));
            }
        }

        if (system is not null)
        {
            if (system.BootTime is { } boot && (now - boot).TotalDays >= AppConfig.RestartAdviceDays)
            {
                actions.Add(new RecommendedAction(
                    ActionKind.Restart,
                    ActionSeverity.Info,
                    Strings.Action_Restart,
                    Formats.Template(Strings.Action_RestartDetail, Formats.Whole((int)(now - boot).TotalDays)),
                    Strings.Action_See,
                    PageKey.System));
            }

            actions.Add(SecurityAction(system.Security));
        }

        return actions;
    }

    private static RecommendedAction SecurityAction(SecuritySummary security)
    {
        // An unreadable item (BitLocker without administrator rights) is neither active nor inactive: omitted.
        var items = new List<(string Name, bool? Active)>
        {
            (Strings.Security_Defender, security.DefenderActive),
            (Strings.Security_Firewall, security.FirewallActive),
            (Strings.Security_BitLocker, security.BitLockerSystem),
            (Strings.Security_SecureBoot, security.SecureBoot),
        };

        var inactive = items.Where(i => i.Active == false).Select(i => i.Name).ToList();
        if (inactive.Count > 0)
        {
            return new RecommendedAction(
                ActionKind.SecurityCheck,
                ActionSeverity.Warning,
                Formats.Plural(inactive.Count, Strings.Action_SecurityCheckOne, Strings.Action_SecurityCheckMany),
                string.Join(", ", inactive) + ".",
                Strings.Action_See,
                PageKey.System);
        }

        var active = items.Where(i => i.Active == true).Select(i => i.Name).ToList();
        return new RecommendedAction(
            ActionKind.SecurityOk,
            ActionSeverity.Success,
            Strings.Action_SecurityOk,
            Formats.JoinWithAnd(active) + ".",
            Strings.Action_See,
            PageKey.System);
    }
}
