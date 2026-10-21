namespace Riftcaster.Core.Diagnostics;

/// <summary>
/// Where the log files are (see LogFiles in the server), for the Windows launcher and the Settings
/// page to point people to.
/// </summary>
/// <param name="Path">The folder's full path.</param>
public sealed record LogFolder(string Path)
{
    /// <summary>
    /// How many log files are kept: the most recent ones, a file for each day Riftcaster ran. Older
    /// ones are deleted as new ones start.
    /// </summary>
    public const int RetainedFiles = 7;
}
