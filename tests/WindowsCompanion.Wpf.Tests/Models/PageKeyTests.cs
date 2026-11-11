using WindowsCompanion.Models;

namespace WindowsCompanion.Tests.Models;

public sealed class PageKeyTests
{
    [Fact]
    public void Lit_la_page_demandee_en_ligne_de_commande()
    {
        Assert.Equal(PageKey.Network, PageKeys.FromArguments(["--replace=42", "--page=reseau"]));
    }

    [Theory]
    [InlineData("inconnue")]
    [InlineData(null)]
    public void Ouvre_la_vue_d_ensemble_pour_un_identifiant_inconnu(string? id) =>
        Assert.Equal(PageKey.Overview, PageKeys.Parse(id));

    [Fact]
    public void Fait_l_aller_retour_entre_cle_et_identifiant()
    {
        foreach (var key in Enum.GetValues<PageKey>())
        {
            Assert.Equal(key, PageKeys.Parse(PageKeys.ToId(key)));
        }
    }
}
