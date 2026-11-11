using WindowsCompanion.Models;
using WindowsCompanion.Services;
using WindowsCompanion.Tests.Support;

namespace WindowsCompanion.Tests.Services;

public sealed class RecommendationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 10, 0, 0, TimeSpan.Zero);
    private readonly RecommendationService _service = new();

    [Fact]
    public void Ne_propose_rien_sur_les_mises_a_jour_avant_la_premiere_recherche()
    {
        var actions = _service.Compute(UpdatesSummary.NotSearched, null, null, Now);

        Assert.Empty(actions);
    }

    [Fact]
    public void Range_les_actions_dans_l_ordre_du_cahier()
    {
        var updates = new UpdatesSummary(true, 10, 1471 * Samples.Mib, false, "Docker Desktop", 590 * Samples.Mib);
        var hardware = Samples.Hardware(Samples.Volume("C:", 931, 612), Samples.Volume("E:", 3726, 3220), Samples.Volume("F:", 100, 95));
        var system = Samples.System(boot: Now.AddDays(-12));

        var actions = _service.Compute(updates, hardware, system, Now);

        Assert.Equal([ActionKind.Updates, ActionKind.VolumeFull, ActionKind.VolumeFull, ActionKind.Restart, ActionKind.SecurityOk], actions.Select(a => a.Kind));
        Assert.Equal("Le volume F: est plein à 95 %", actions[1].Title);
        Assert.Equal("10 mises à jour disponibles", actions[0].Title);
    }

    [Fact]
    public void Propose_applications_a_jour_sans_mise_a_jour()
    {
        var action = Assert.Single(_service.Compute(new UpdatesSummary(true, 0, 0, false, null, 0), null, null, Now));

        Assert.Equal(ActionKind.UpToDate, action.Kind);
    }

    [Fact]
    public void Liste_les_points_de_securite_inactifs_et_ignore_bitlocker_illisible()
    {
        var system = Samples.System(boot: Now, security: new SecuritySummary(false, true, false, null));

        var action = Assert.Single(_service.Compute(UpdatesSummary.NotSearched, null, system, Now));

        Assert.Equal(ActionKind.SecurityCheck, action.Kind);
        Assert.Equal("Sécurité : 2 points à vérifier", action.Title);
        Assert.Equal("Defender, démarrage sécurisé.", action.Detail);
    }
}
