namespace Riftcaster.Server;

/// <summary>
/// Where the log files are (see LogFiles), for the Windows launcher to point people to.
/// </summary>
/// <param name="Path">The folder's full path.</param>
public sealed record LogFolder(string Path);
