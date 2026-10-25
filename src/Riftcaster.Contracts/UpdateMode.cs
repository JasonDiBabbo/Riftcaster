using System.Text.Json.Serialization;

namespace Riftcaster.Contracts;

/// <summary>
/// What Riftcaster does about a newer version.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<UpdateMode>))]
public enum UpdateMode
{
    /// <summary>Downloads quietly and installs when Riftcaster quits, never in the middle of a show.</summary>
    Automatic,

    /// <summary>Downloads quietly, but installs only when Restart to update is chosen.</summary>
    AskFirst,

    /// <summary>Never checks or installs.</summary>
    Off,
}
