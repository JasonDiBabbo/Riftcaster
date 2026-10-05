namespace Riftcaster.Server;

/// <summary>
/// Where the server keeps its saved state: players, lower thirds, match settings, the featured
/// card, the card catalogue and the access code (issue #59).
/// </summary>
/// <remarks>
/// A published build keeps it in the user's own app-data folder (%APPDATA%\Riftcaster on Windows),
/// not in its own folder, so replacing the app with a new version, by hand or by an installer,
/// never touches it. Not %LOCALAPPDATA%: installers such as Velopack put the app there and remove
/// that folder when uninstalling. Development keeps the project's data folder, as before.
/// </remarks>
internal static class DataFolder
{
    /// <summary>
    /// The setting that chooses the folder, absolute or relative to the content root.
    /// </summary>
    public const string Setting = "Storage:DataDirectory";

    /// <summary>
    /// The saved-state folder for this run.
    /// </summary>
    /// <param name="configured">The Storage:DataDirectory setting, or null when it isn't set.</param>
    /// <param name="environment">The app's environment: Development, or a published build's Production.</param>
    /// <param name="appData">
    /// The user's app-data folder, from Environment.SpecialFolder.ApplicationData; empty when there
    /// isn't one (some service accounts), which falls back to the content root.
    /// </param>
    /// <returns>The folder's full path.</returns>
    public static string Resolve(string? configured, IHostEnvironment environment, string appData)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(Path.Combine(environment.ContentRootPath, configured));
        }

        return environment.IsDevelopment() || string.IsNullOrEmpty(appData)
            ? Path.Combine(environment.ContentRootPath, "data")
            : Path.Combine(appData, "Riftcaster");
    }
}
