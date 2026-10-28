namespace Riftcaster.Contracts;

/// <summary>
/// The Settings page's card catalog section.
/// </summary>
/// <remarks>
/// A property with a default, not a constructor parameter: a settings file saved before a setting
/// existed still loads, and the setting starts at its default.
/// </remarks>
public record CardSettings
{
    /// <summary>
    /// The card database the catalog is in use from. Changed once a new source's catalog has been
    /// downloaded (or was already saved), not when it's asked for.
    /// </summary>
    public CardSourceKind Source { get; init; } = CardSourceKind.Riftcodex;
}
