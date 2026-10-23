namespace Riftcaster.Contracts;

/// <summary>
/// The Settings page's server section.
/// </summary>
/// <remarks>
/// A property with a default, not a constructor parameter: a settings file saved before a setting
/// existed still loads, and the setting starts at its default.
/// </remarks>
public record ServerSettings
{
    /// <summary>
    /// The port the web interface and the overlays are served on, from the next start. Null until
    /// someone chooses one: the default (5062) then applies. A <c>--urls</c> launch option wins over it.
    /// </summary>
    public int? Port { get; init; }
}
