using System.Globalization;
using System.Windows.Data;
using WindowsCompanion.Converters;
using WindowsCompanion.Models;

namespace WindowsCompanion.Tests.Converters;

public sealed class EnumEqualsConverterTests
{
    private readonly EnumEqualsConverter _converter = new();

    [Fact]
    public void Coche_le_segment_de_la_valeur_courante()
    {
        Assert.Equal(true, _converter.Convert(ThemeMode.Dark, typeof(bool), ThemeMode.Dark, CultureInfo.InvariantCulture));
        Assert.Equal(false, _converter.Convert(ThemeMode.Light, typeof(bool), ThemeMode.Dark, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Renvoie_la_valeur_du_segment_coche_et_ignore_le_decochage()
    {
        Assert.Equal(ThemeMode.Light, _converter.ConvertBack(true, typeof(ThemeMode), ThemeMode.Light, CultureInfo.InvariantCulture));
        Assert.Equal(Binding.DoNothing, _converter.ConvertBack(false, typeof(ThemeMode), ThemeMode.Light, CultureInfo.InvariantCulture));
    }
}
