using WindowsCompanion.Helpers;

namespace WindowsCompanion.Tests.Helpers;

public sealed class FormatsTests
{
    private const char Nbsp = ' ';
    private const long Mib = 1024L * 1024;
    private const long Gib = 1024L * 1024 * 1024;

    [Theory]
    [InlineData(1_677_722L, "1,6 Mo")]
    [InlineData(78L * Mib, "78 Mo")]
    [InlineData(1_471L * Mib, "1,4 Go")]
    public void Formate_une_taille_de_telechargement(long bytes, string expected) =>
        Assert.Equal(expected, Formats.Size(bytes));

    [Fact]
    public void Separe_les_milliers_par_une_espace_fine_insecable() =>
        Assert.Equal("1 190", Formats.Whole(1190));

    [Fact]
    public void Arrondit_un_pourcentage_avec_espace_insecable() =>
        Assert.Equal("66" + Nbsp + "%", Formats.Percent(65.7));

    [Fact]
    public void Ajoute_ou_plus_quand_une_taille_est_inconnue() =>
        Assert.Equal("1,4 Go ou plus", Formats.DownloadTotal((long)(1.4 * Gib), partial: true));

    [Theory]
    [InlineData(42, "42 s")]
    [InlineData(190, "3 min 10 s")]
    [InlineData(3900, "1 h 05 min")]
    public void Formate_une_duree(int seconds, string expected) =>
        Assert.Equal(expected, Formats.Duration(seconds));

    [Fact]
    public void Ecrit_aujourd_hui_et_hier_puis_le_jour_et_le_mois()
    {
        var now = new DateTimeOffset(2026, 11, 8, 15, 0, 0, TimeSpan.Zero).ToLocalTime();
        var today = new DateTimeOffset(now.Year, now.Month, now.Day, 8, 12, 0, now.Offset);

        Assert.Equal("Aujourd'hui 08:12", Formats.Relative(today, now, capitalize: true));
        Assert.Equal("hier 08:12", Formats.Relative(today.AddDays(-1), now, capitalize: false));
        Assert.Equal("3 nov. 08:12", Formats.Relative(today.AddDays(-5), now, capitalize: true));
        Assert.Equal("3 nov. 2025 08:12", Formats.Relative(today.AddDays(-5).AddYears(-1), now, capitalize: true));
    }

    [Fact]
    public void Accorde_le_pluriel_a_partir_de_deux()
    {
        Assert.Equal("1 mise à jour", Formats.Plural(1, "{0} mise à jour", "{0} mises à jour"));
        Assert.Equal("0 mise à jour", Formats.Plural(0, "{0} mise à jour", "{0} mises à jour"));
        Assert.Equal("2 mises à jour", Formats.Plural(2, "{0} mise à jour", "{0} mises à jour"));
    }

    [Fact]
    public void Formate_le_temps_de_fonctionnement()
    {
        Assert.Equal("12 jours 5 heures", Formats.Uptime(new TimeSpan(12, 5, 0, 0)));
        Assert.Equal("1 heure", Formats.Uptime(TimeSpan.FromHours(1)));
    }

    [Theory]
    [InlineData("3c5282-1a9f04", "3C-52-82-1A-9F-04")]
    [InlineData("", null)]
    public void Normalise_une_adresse_mac(string raw, string? expected) =>
        Assert.Equal(expected, Formats.Mac(raw));

    [Fact]
    public void Formate_les_debits_et_frequences()
    {
        Assert.Equal("1" + Nbsp + "Gb/s", Formats.LinkSpeed(1_000_000_000));
        Assert.Equal("2,5" + Nbsp + "Gb/s", Formats.LinkSpeed(2_500_000_000));
        Assert.Equal("100" + Nbsp + "Mb/s", Formats.LinkSpeed(100_000_000));
        Assert.Equal(string.Empty, Formats.LinkSpeed(null));
        Assert.Equal("2,40" + Nbsp + "GHz", Formats.Ghz(2400));
    }

    [Fact]
    public void Joint_avec_et_avant_le_dernier_element()
    {
        Assert.Equal("a, b et c", Formats.JoinWithAnd(["a", "b", "c"]));
        Assert.Equal("a", Formats.JoinWithAnd(["a"]));
    }

    [Fact]
    public void Indique_demarre_aujourd_hui_sous_un_jour()
    {
        Assert.Equal("démarré aujourd'hui", Formats.StartedAgo(TimeSpan.FromHours(3)));
        Assert.Equal("démarré il y a 12 jours", Formats.StartedAgo(TimeSpan.FromDays(12.5)));
    }
}
