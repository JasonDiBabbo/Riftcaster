using System.ComponentModel;
using Microsoft.Extensions.Logging;

namespace Riftcaster.Core.Diagnostics;

/// <summary>
/// How an attempt to open the log folder ended.
/// </summary>
public enum FolderOpenResult
{
    /// <summary>The file manager was asked to show the folder.</summary>
    Opened,

    /// <summary>This computer has no desktop to show it on, so nothing was tried.</summary>
    NoDesktop,

    /// <summary>The file manager couldn't be started.</summary>
    Failed,
}

/// <summary>
/// Opens the log folder in the computer's file manager (Explorer, Finder and so on), for the
/// Settings page's Open folder button.
/// </summary>
/// <remarks>
/// It only ever opens the log folder: nothing the caller passes picks what is opened. It opens on
/// the computer running the server, so a page opened from another device mustn't offer it.
/// </remarks>
/// <param name="folder">The log folder.</param>
/// <param name="desktopExists">Whether the computer has a desktop session (see <see cref="DesktopSession"/>).</param>
/// <param name="open">Asks the computer to show a folder in its file manager.</param>
/// <param name="logger">Logs a failed attempt.</param>
public sealed class LogFolderOpener(LogFolder folder, Func<bool> desktopExists, Action<string> open, ILogger<LogFolderOpener> logger)
{
    /// <summary>
    /// The log folder's full path.
    /// </summary>
    public string FolderPath => folder.Path;

    /// <summary>
    /// Whether <see cref="Open"/> is worth trying: this computer has a desktop session.
    /// </summary>
    public bool CanOpen => desktopExists();

    /// <summary>
    /// Opens the log folder. Never throws: a failure is logged and returned.
    /// </summary>
    /// <returns>How it ended.</returns>
    public FolderOpenResult Open()
    {
        if (!desktopExists())
        {
            logger.LogInformation("Not opening the log folder {Folder}: there is no desktop session.", folder.Path);
            return FolderOpenResult.NoDesktop;
        }

        try
        {
            // Nothing has been logged yet in a copy that's just started, so the folder may not exist.
            Directory.CreateDirectory(folder.Path);
            open(folder.Path);
            return FolderOpenResult.Opened;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            logger.LogWarning(exception, "Couldn't open the log folder {Folder}.", folder.Path);
            return FolderOpenResult.Failed;
        }
    }
}
