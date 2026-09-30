using System.Text.Json.Serialization;

namespace Riftcaster.Contracts;

/// <summary>
/// The platforms a Socials lower third can link to, one per icon in the overlay design.
/// </summary>
/// <remarks>
/// Serialized by name ("Twitch"), not number, so reordering or adding values can't
/// silently change what a saved entry or the overlay means.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<SocialNetwork>))]
public enum SocialNetwork
{
    Twitch,
    YouTube,
    Instagram,
    TikTok,
    X,
    Discord,
}
