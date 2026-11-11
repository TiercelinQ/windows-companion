using System.Globalization;

namespace WindowsCompanion.Helpers;

/// <summary>Application tile of the update table: two letters and a stable color (F-MAJ-10).</summary>
public static class AppTile
{
    private const uint FnvOffset = 2166136261;
    private const uint FnvPrime = 16777619;

    /// <summary>Gets the first two alphanumeric characters of a name, capital then lowercase.</summary>
    /// <param name="name">Application name.</param>
    /// <returns>For example "Mo" for "Mozilla Firefox", "7z" for "7-Zip".</returns>
    public static string Initials(string name)
    {
        var letters = (name ?? string.Empty).Where(char.IsLetterOrDigit).Take(2).ToArray();
        return letters.Length switch
        {
            0 => "?",
            1 => char.ToUpper(letters[0], CultureInfo.CurrentCulture).ToString(),
            _ => string.Concat(char.ToUpper(letters[0], CultureInfo.CurrentCulture), char.ToLower(letters[1], CultureInfo.CurrentCulture)),
        };
    }

    /// <summary>Gets a stable color index from the package identifier (FNV-1a), so a package keeps its color between searches.</summary>
    /// <param name="id">Package identifier.</param>
    /// <returns>An index between 0 and the tile color count minus one.</returns>
    public static int ColorIndex(string id)
    {
        var hash = FnvOffset;
        foreach (var c in (id ?? string.Empty).ToUpperInvariant())
        {
            hash = unchecked((hash ^ c) * FnvPrime);
        }

        return (int)(hash % AppConfig.AppTileColorCount);
    }
}
