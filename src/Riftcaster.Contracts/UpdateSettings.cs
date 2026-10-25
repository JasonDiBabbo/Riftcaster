namespace Riftcaster.Contracts;

/// <summary>
/// The Settings page's updates section.
/// </summary>
/// <remarks>
/// Properties with defaults, not constructor parameters: a settings file saved before a setting
/// existed still loads, and the setting starts at its default.
/// </remarks>
public record UpdateSettings
{
    /// <summary>What Riftcaster does about a newer version.</summary>
    public UpdateMode Mode { get; init; } = UpdateMode.Automatic;

    /// <summary>
    /// Which releases to look for. Null until someone chooses: a pre-release build then keeps to the
    /// pre-releases it was installed from, and any other to the release versions.
    /// </summary>
    public UpdateChannel? Channel { get; init; }
}
