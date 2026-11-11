using WindowsCompanion.Models;
using WindowsCompanion.Services;

namespace WindowsCompanion.Tests.Services;

public sealed class ElevationServiceTests
{
    [Fact]
    public void Relance_sur_la_meme_page_et_attend_l_instance_remplacee() =>
        Assert.Equal(["--page=reseau", "--replace=4242"], ElevationService.BuildArguments(PageKey.Network, 4242));
}
