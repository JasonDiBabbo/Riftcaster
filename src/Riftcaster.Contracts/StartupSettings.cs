namespace Riftcaster.Contracts;

/// <summary>
/// The Settings page's startup section.
/// </summary>
/// <remarks>
/// A property with a default, not a constructor parameter: a settings file saved before a setting
/// existed still loads, and the setting starts at its default. Whether Riftcaster starts with Windows isn't
/// kept here: it's whether the entry exists in Windows, which the user can also change there.
/// </remarks>
public record StartupSettings
{
    /// <summary>
    /// Whether starting Riftcaster opens its web interface in the browser. A <c>--Launcher:OpenWebUI</c>
    /// launch option wins over it.
    /// </summary>
    public bool OpenWebUI { get; init; } = true;
}
