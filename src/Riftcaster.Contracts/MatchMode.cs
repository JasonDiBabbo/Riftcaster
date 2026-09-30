using System.Text.Json.Serialization;

namespace Riftcaster.Contracts;

/// <summary>
/// The mode of play, which determines the number
/// of participating players and whether there are teams.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<MatchMode>))]
public enum MatchMode
{
    OneVsOne,
    TwoVsTwo,
    FreeForAll3,
    FreeForAll4,
}
