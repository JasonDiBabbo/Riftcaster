namespace Riftcaster.Core.Startup;

/// <summary>
/// Whether Riftcaster can be made to start with Windows from this copy, and if not, why: the Settings page says which.
/// </summary>
public enum StartupAvailability
{
    /// <summary>It can: the tray app of an installed copy.</summary>
    Available,

    /// <summary>It's the console server, which has no tray app to start: the Startup section isn't for it.</summary>
    ConsoleServer,

    /// <summary>It's the tray app running from source, where an entry would point at a build that gets replaced or deleted.</summary>
    NotInstalled,

    /// <summary>
    /// It's the portable copy, which can be moved or taken off a USB stick: an entry would stop working, and would replace
    /// the installed copy's, which the two share a name for.
    /// </summary>
    Portable,
}
