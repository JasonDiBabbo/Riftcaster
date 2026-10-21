namespace Riftcaster.Contracts;

/// <summary>
/// The Settings page's logging section.
/// </summary>
/// <remarks>
/// A property with a default, not a constructor parameter: a settings file saved before a setting
/// existed still loads, and the setting starts at its default.
/// </remarks>
public record LoggingSettings
{
    /// <summary>How much the server writes to its log.</summary>
    public LogDetail Detail { get; init; } = LogDetail.Normal;
}
