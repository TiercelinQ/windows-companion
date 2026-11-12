using WindowsCompanion.Models;
using WindowsCompanion.Services.Winget;

namespace WindowsCompanion.Tests.Services.Winget;

public sealed class WingetOutputParserTests
{
    private static string Row(string name, string id, string version, string available, string source) =>
        name.PadRight(27) + id.PadRight(27) + version.PadRight(9) + available.PadRight(11) + source;

    private static string Table(string header, params string[] rows) =>
        string.Join('\n', new[] { "\x1B[2K\r   - ", header, new string('-', 80) }.Concat(rows).Append("2 upgrades available."));

    [Fact]
    public void Lit_le_tableau_anglais()
    {
        var output = Table(
            Row("Name", "Id", "Version", "Available", "Source"),
            Row("Logitech G HUB", "Logitech.GHUB", "2026.3.8", "2026.7.9", "winget"),
            Row("Ubisoft Connect", "Ubisoft.Connect", "< 175.1", "175.1.0.1", "winget"));

        var items = WingetOutputParser.ParseUpgradeTable(output, UpdateSource.Winget);

        Assert.Equal(2, items.Count);
        Assert.Equal(new UpdateItem("Logitech.GHUB", "Logitech G HUB", "2026.3.8", "2026.7.9", UpdateSource.Winget, null), items[0]);
        Assert.Equal("< 175.1", items[1].InstalledVersion);
    }

    [Fact]
    public void Lit_le_tableau_francais_par_positions_de_colonnes()
    {
        var output = Table(
            Row("Nom", "ID", "Version", "Disponible", "Source"),
            Row("Mozilla Firefox (fr)", "Mozilla.Firefox.fr", "148.0.2", "150.0", "winget"));

        var items = WingetOutputParser.ParseUpgradeTable(output, UpdateSource.Winget);

        var item = Assert.Single(items);
        Assert.Equal("Mozilla Firefox (fr)", item.Name);
        Assert.Equal("Mozilla.Firefox.fr", item.Id);
        Assert.Equal("150.0", item.AvailableVersion);
    }

    [Fact]
    public void Renvoie_une_liste_vide_sans_tableau()
    {
        Assert.Empty(WingetOutputParser.ParseUpgradeTable("Aucune mise à jour disponible.", UpdateSource.MsStore));
    }

    [Theory]
    [InlineData("  ██████████▒▒▒▒▒▒▒▒  12.5 MB / 25.0 MB", true)]
    [InlineData("  ██████████  42%", true)]
    [InlineData("Le code de hachage de l'installation a été vérifié avec succès", false)]
    [InlineData("Démarrage du package d'installation...", false)]
    public void Reconnait_une_ligne_de_progression_du_telechargement(string line, bool expected) =>
        Assert.Equal(expected, WingetOutputParser.IsProgressLine(line));

    [Fact]
    public void Ignore_le_bruit_de_la_console()
    {
        Assert.True(WingetOutputParser.IsNoise(" \\ "));
        Assert.True(WingetOutputParser.IsNoise("██████▒▒▒"));
        Assert.False(WingetOutputParser.IsNoise("Installation réussie"));
        Assert.Equal("texte", WingetOutputParser.Clean("\x1B[32mtexte\x1B[0m  "));
    }

    [Theory]
    [InlineData(1603, "1603")]
    [InlineData(unchecked((int)0x8A150011), "0x8A150011")]
    public void Formate_les_codes_de_sortie(int code, string expected) =>
        Assert.Equal(expected, WingetOutputParser.FormatExitCode(code));

    [Fact]
    public void Lit_la_version_de_winget() =>
        Assert.Equal("1.12.350", WingetOutputParser.ParseVersion("v1.12.350\r\n"));

    [Fact]
    public void Considere_aucune_mise_a_jour_comme_un_resultat_normal()
    {
        Assert.True(WingetOutputParser.IsEmptyResult(0));
        Assert.True(WingetOutputParser.IsEmptyResult(unchecked((int)0x8A15002B)));
        Assert.False(WingetOutputParser.IsEmptyResult(1));
    }
}
