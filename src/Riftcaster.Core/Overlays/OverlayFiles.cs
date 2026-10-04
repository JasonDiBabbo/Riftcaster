namespace Riftcaster.Core.Overlays;

/// <summary>
/// Where the built overlay pages are, and whether they're there, so the server can serve them and
/// the admin can warn when it can't.
/// </summary>
/// <remarks>
/// A missing folder doesn't stop the server: the admin still works. But every OBS source would be
/// blank, so the admin header says so rather than leaving it to a line in the console.
/// </remarks>
/// <param name="Folder">The folder's full path, or null when no folder is configured.</param>
/// <param name="Found">True when the folder exists, so the overlays are served.</param>
public sealed record OverlayFiles(string? Folder, bool Found)
{
    /// <summary>
    /// Finds the overlays folder from its configured path.
    /// </summary>
    /// <param name="contentRoot">The folder a relative <paramref name="configuredPath"/> is relative to.</param>
    /// <param name="configuredPath">The Overlays:Path setting, absolute or relative; null or empty when it isn't set.</param>
    /// <returns>Where the folder is, and whether it exists.</returns>
    public static OverlayFiles Find(string contentRoot, string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return new OverlayFiles(null, false);
        }

        var folder = Path.GetFullPath(Path.Combine(contentRoot, configuredPath));
        return new OverlayFiles(folder, Directory.Exists(folder));
    }
}
