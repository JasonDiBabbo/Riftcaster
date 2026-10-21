namespace Riftcaster.Core.Diagnostics;

/// <summary>
/// Whether this computer has a desktop to open a folder in: something to show a file manager on.
/// A best guess: it can be wrong, so what opens a folder must still cope with failing.
/// </summary>
public static class DesktopSession
{
    /// <summary>
    /// Whether this computer has a desktop session, as far as can be told.
    /// </summary>
    public static bool Current => Exists(
        OperatingSystem.IsWindows(),
        OperatingSystem.IsMacOS(),
        Environment.UserInteractive,
        Environment.GetEnvironmentVariable,
        command => OnPath(command, Environment.GetEnvironmentVariable("PATH")));

    /// <summary>
    /// Works out whether a desktop session exists from what the computer reports.
    /// </summary>
    /// <param name="isWindows">Whether this is Windows.</param>
    /// <param name="isMacOS">Whether this is macOS. Neither this nor Windows means Linux or another Unix.</param>
    /// <param name="userInteractive">
    /// Whether the process runs in a user's session, not as a service. Only Windows tells, and only
    /// there does it matter.
    /// </param>
    /// <param name="environment">Reads an environment variable, or returns null if it isn't set.</param>
    /// <param name="commandExists">Whether a command is on the path.</param>
    public static bool Exists(bool isWindows, bool isMacOS, bool userInteractive, Func<string, string?> environment, Func<string, bool> commandExists)
    {
        if (isWindows)
        {
            // A service has no desktop of its own to show anything on.
            return userInteractive;
        }

        if (isMacOS)
        {
            // Every Mac has one, and `open` shows it; a Mac run over SSH is rare enough not to guess about.
            return true;
        }

        // Linux and the rest: a graphical session sets one of these, and xdg-open is what opens things in it.
        // Over SSH with X forwarding, DISPLAY is set and the folder opens on the screen it's forwarded to.
        var graphical = !string.IsNullOrEmpty(environment("DISPLAY")) || !string.IsNullOrEmpty(environment("WAYLAND_DISPLAY"));
        return graphical && commandExists("xdg-open");
    }

    /// <summary>
    /// Whether a command is a file in one of the folders on the path.
    /// </summary>
    /// <param name="command">The command's file name.</param>
    /// <param name="path">The PATH environment variable, or null if it isn't set.</param>
    public static bool OnPath(string command, string? path) =>
        (path ?? "")
            .Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(folder => File.Exists(System.IO.Path.Combine(folder, command)));
}
