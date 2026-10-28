using System.Text.Json.Serialization;

namespace Riftcaster.Contracts;

/// <summary>
/// Which card database the catalog is fetched from.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<CardSourceKind>))]
public enum CardSourceKind
{
    /// <summary>Riftcodex, fetched page by page.</summary>
    Riftcodex,

    /// <summary>RiftHunt, fetched as one download at most once a day.</summary>
    RiftHunt,
}
