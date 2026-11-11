using System.Globalization;
using System.Windows;
using WindowsCompanion.Converters;

namespace WindowsCompanion.Tests.Converters;

public sealed class AppTileBrushConverterTests
{
    [Fact]
    public void Ne_renvoie_rien_sans_dictionnaire_de_ressources()
    {
        // No WPF application in the test process: the resource lookup has nothing to find.
        var result = new AppTileBrushConverter().Convert(42, typeof(object), null, CultureInfo.InvariantCulture);

        Assert.Equal(DependencyProperty.UnsetValue, result);
    }
}
