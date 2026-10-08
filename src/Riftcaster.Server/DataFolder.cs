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
/// <para>
/// A portable copy (the release's portable zip, #62) keeps it in a data folder of its own instead,
/// so its data goes wherever the folder goes, such as a USB stick, and never mixes with an installed
/// copy's. It's beside the app's folder (current), not in it, since each update replaces current.
/// </para>
/// </remarks>
internal static class DataFolder
{
    /// <summary>
    /// The setting that chooses the folder, absolute or relative to the content root.
    /// </summary>
    public const string Setting = "Storage:DataDirectory";

    /// <summary>
    /// The file Velopack leaves in a portable copy's folder, beside the app's own (current).
    /// </summary>
    public const string PortableMarker = ".portable";

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

        if (environment.IsDevelopment())
        {
            return Path.Combine(environment.ContentRootPath, "data");
        }

        var portableFolder = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(environment.ContentRootPath));
        if (portableFolder is not null && File.Exists(Path.Combine(portableFolder, PortableMarker)))
        {
            return Path.Combine(portableFolder, "data");
        }

        return string.IsNullOrEmpty(appData)
            ? Path.Combine(environment.ContentRootPath, "data")
            : Path.Combine(appData, "Riftcaster");
    }
}
