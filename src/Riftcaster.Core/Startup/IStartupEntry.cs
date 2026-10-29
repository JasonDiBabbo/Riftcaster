namespace Riftcaster.Core.Startup;

/// <summary>
/// What starts Riftcaster when someone signs in to Windows: an entry kept by Windows itself, so it can
/// also be removed there (Task Manager's Startup tab, say).
/// </summary>
/// <remarks>
/// The Windows app's implementation; a test supplies its own, so no test touches the real registry.
/// </remarks>
public interface IStartupEntry
{
    /// <summary>
    /// Whether the entry is there now.
    /// </summary>
    bool Exists { get; }

    /// <summary>
    /// Adds the entry, or removes it. Either is harmless when it's already so.
    /// </summary>
    /// <param name="on">Whether Riftcaster starts when someone signs in.</param>
    void Set(bool on);
}
