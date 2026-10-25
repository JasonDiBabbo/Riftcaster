namespace Riftcaster.Core.Updates;

/// <summary>
/// Whether this copy of Riftcaster can update itself, and if not, why: the Settings page says which.
/// </summary>
public enum UpdateAvailability
{
    /// <summary>It can: the tray app of an installed or portable copy.</summary>
    Updatable,

    /// <summary>It's running from source, such as with <c>dotnet run</c>: update it with git.</summary>
    FromSource,

    /// <summary>It's the console server from an installed folder: updates are the tray app's job.</summary>
    ConsoleInInstalledFolder,

    /// <summary>It's an installed copy with updates turned off in its configuration (<c>Updates:Source</c> empty).</summary>
    TurnedOffInConfiguration,
}
