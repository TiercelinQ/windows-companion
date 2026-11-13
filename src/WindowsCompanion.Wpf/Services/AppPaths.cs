using System.IO;

namespace WindowsCompanion.Services;

/// <summary>Application locations, resolved once (rules/security.md §1).</summary>
public interface IAppPaths
{
    /// <summary>Gets the user data folder, <c>%APPDATA%\WindowsCompanion</c>.</summary>
    string UserData { get; }

    /// <summary>Gets the full path of <c>preferences.json</c>.</summary>
    string PreferencesFile { get; }

    /// <summary>Gets the full path of <c>history.json</c>.</summary>
    string HistoryFile { get; }

    /// <summary>Gets the user's Documents folder, destination of the exports.</summary>
    string Documents { get; }

    /// <summary>Gets the folder of the log files.</summary>
    string Logs { get; }
}

/// <summary>Default implementation of <see cref="IAppPaths"/>.</summary>
public sealed class AppPaths : IAppPaths
{
    /// <inheritdoc />
    public string UserData { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppConfig.AppName);

    /// <inheritdoc />
    public string PreferencesFile => Path.Combine(UserData, AppConfig.PreferencesFileName);

    /// <inheritdoc />
    public string HistoryFile => Path.Combine(UserData, AppConfig.HistoryFileName);

    /// <inheritdoc />
    public string Documents { get; } = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    /// <inheritdoc />
    public string Logs => Path.Combine(UserData, AppConfig.LogsFolderName);
}
