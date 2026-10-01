namespace Riftcaster.Contracts;

/// <summary>
/// What the match overlays show: the match's rules, and every player and team.
/// </summary>
/// <remarks>
/// Sent as one event, so an overlay never combines players from one moment with rules from another
/// (for example, a score of 10 against a points to win that has just been lowered to 8).
/// </remarks>
/// <param name="Settings">The match's rules, which decide how many players are in use and whether they play as teams.</param>
/// <param name="Players">Every player and team.</param>
public record MatchState(MatchSettings Settings, PlayersState Players);
