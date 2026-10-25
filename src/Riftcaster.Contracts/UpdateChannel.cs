using System.Text.Json.Serialization;

namespace Riftcaster.Contracts;

/// <summary>
/// Which releases Riftcaster looks for.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<UpdateChannel>))]
public enum UpdateChannel
{
    /// <summary>Release versions only.</summary>
    Stable,

    /// <summary>Includes beta builds, which can have rough edges.</summary>
    PreRelease,
}
