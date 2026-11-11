using WindowsCompanion.Models;
using WindowsCompanion.Tests.Support;

namespace WindowsCompanion.Tests.Models;

public sealed class UsageLevelTests
{
    [Theory]
    [InlineData(79.9, UsageLevel.Normal)]
    [InlineData(80, UsageLevel.Attention)]
    [InlineData(89.6, UsageLevel.Attention)]
    [InlineData(90, UsageLevel.Critical)]
    public void Applique_les_seuils_avant_arrondi(double percent, UsageLevel expected) =>
        Assert.Equal(expected, UsageLevels.From(percent));

    [Fact]
    public void Calcule_le_niveau_d_un_volume_sur_les_octets()
    {
        var volume = Samples.Volume("E:", 3726, 3220);

        Assert.Equal(UsageLevel.Attention, volume.Level);
        Assert.Equal(506 * Samples.Gib, volume.FreeBytes);
    }
}
