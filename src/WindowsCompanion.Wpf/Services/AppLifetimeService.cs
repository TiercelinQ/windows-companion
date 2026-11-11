using System.Windows;

namespace WindowsCompanion.Services;

/// <summary>Closes the application through its main window, so the normal close sequence runs (D-07).</summary>
public interface IAppLifetimeService
{
    /// <summary>Closes the main window; the application exits once it is closed.</summary>
    void CloseMainWindow();
}

/// <summary>WPF implementation of <see cref="IAppLifetimeService"/>.</summary>
public sealed class AppLifetimeService : IAppLifetimeService
{
    /// <inheritdoc />
    public void CloseMainWindow() => Application.Current?.MainWindow?.Close();
}
