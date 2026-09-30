using System.Text.Json.Serialization;

namespace Riftcaster.Contracts;

/// <summary>
/// How many game wins it takes to win.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<MatchFormat>))]
public enum MatchFormat
{
    BestOf1,
    BestOf3,
}
