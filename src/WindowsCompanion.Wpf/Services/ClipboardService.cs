using System.Runtime.InteropServices;
using System.Windows;

namespace WindowsCompanion.Services;

/// <summary>Places text in the clipboard (D-07).</summary>
public interface IClipboardService
{
    /// <summary>Copies a text.</summary>
    /// <param name="text">Text to copy.</param>
    /// <returns>False when the clipboard is held by another application.</returns>
    bool SetText(string text);
}

/// <summary>WPF implementation of <see cref="IClipboardService"/>.</summary>
public sealed class ClipboardService : IClipboardService
{
    /// <inheritdoc />
    public bool SetText(string text)
    {
        try
        {
            Clipboard.SetText(text);
            return true;
        }
        catch (ExternalException)
        {
            return false;
        }
    }
}
