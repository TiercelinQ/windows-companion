using WindowsCompanion.Helpers;

namespace WindowsCompanion.Tests.Helpers;

public sealed class AppTileTests
{
    [Theory]
    [InlineData("Mozilla Firefox", "Mo")]
    [InlineData("7-Zip", "7z")]
    [InlineData("X", "X")]
    [InlineData("", "?")]
    public void Prend_les_deux_premiers_caracteres_alphanumeriques(string name, string expected) =>
        Assert.Equal(expected, AppTile.Initials(name));

    [Fact]
    public void Garde_la_meme_couleur_pour_un_meme_identifiant()
    {
        var first = AppTile.ColorIndex("Mozilla.Firefox");

        Assert.Equal(first, AppTile.ColorIndex("mozilla.firefox"));
        Assert.InRange(first, 0, AppConfig.AppTileColorCount - 1);
    }
}
