using WindowsCompanion.Models;
using WindowsCompanion.Services.Winget;
using WindowsCompanion.Tests.Support;

namespace WindowsCompanion.Tests.Services.Winget;

public sealed class WingetArgumentsTests
{
    [Fact]
    public void Passe_l_identifiant_comme_element_distinct()
    {
        var hostile = Samples.Firefox with { Id = "Mozilla.Firefox & calc.exe" };

        var args = WingetArguments.Install(hostile, silent: true);

        Assert.Contains("Mozilla.Firefox & calc.exe", args);
        Assert.Equal(["upgrade", "--id", "Mozilla.Firefox & calc.exe", "--exact", "--source", "winget", "--silent"], args.Take(7));
    }

    [Fact]
    public void N_ajoute_silent_que_si_le_reglage_est_actif() =>
        Assert.DoesNotContain("--silent", WingetArguments.Install(Samples.Spotify, silent: false));

    [Fact]
    public void Cherche_une_source_et_inclut_les_versions_inconnues_sur_demande()
    {
        Assert.Equal(["upgrade", "--source", "msstore", "--include-unknown", "--accept-source-agreements", "--disable-interactivity"], WingetArguments.Upgrade(UpdateSource.MsStore, includeUnknown: true));
        Assert.DoesNotContain("--include-unknown", WingetArguments.Upgrade(UpdateSource.Winget, includeUnknown: false));
    }

    [Fact]
    public void Affiche_la_commande_dans_le_journal() =>
        Assert.Equal("> winget --version", WingetArguments.Display(WingetArguments.Version));
}
