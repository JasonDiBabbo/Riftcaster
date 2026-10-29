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

    /// <summary>It's the tray app running from source or unzipped, where an entry would point at a build that gets replaced or deleted.</summary>
    NotInstalled,
}
