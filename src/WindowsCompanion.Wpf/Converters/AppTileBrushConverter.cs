using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WindowsCompanion.Converters;

/// <summary>Maps an application tile color index to its <c>AppTile{n}Brush</c> resource (F-MAJ-10).</summary>
public sealed class AppTileBrushConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var index = value is int i && i >= 0 && i < AppConfig.AppTileColorCount ? i : 0;
        return Application.Current?.TryFindResource($"AppTile{index.ToString(CultureInfo.InvariantCulture)}Brush") ?? DependencyProperty.UnsetValue;
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}
