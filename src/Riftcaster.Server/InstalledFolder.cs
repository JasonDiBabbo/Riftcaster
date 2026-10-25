namespace Riftcaster.Server;

/// <summary>
/// Tells an installed copy of Riftcaster from one running from source.
/// </summary>
/// <remarks>
/// Velopack installs the app in a <c>current</c> folder, next to the <c>Update.exe</c> that installs updates.
/// A copy that has that beside its folder was installed (or unzipped from the portable zip).
/// </remarks>
internal static class InstalledFolder
{
    /// <summary>
    /// Whether the folder the server runs from belongs to an installed copy.
    /// </summary>
    /// <param name="baseDirectory">The folder of the running program.</param>
    public static bool HasUpdater(string baseDirectory)
    {
        var parent = Directory.GetParent(Path.TrimEndingDirectorySeparator(baseDirectory));
        return parent is not null && File.Exists(Path.Combine(parent.FullName, "Update.exe"));
    }
}
