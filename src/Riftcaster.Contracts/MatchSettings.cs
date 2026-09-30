namespace Riftcaster.Contracts;

/// <summary>
/// The match's rules, set in the admin's Match panel. They drive the players panel,
/// the timer's default length and the overlays.
/// </summary>
/// <param name="PointsToWin">Points a player (or team, in 2v2) needs to win a game.</param>
/// <param name="Format">Best of 1 or best of 3.</param>
/// <param name="Mode">Which players take part, and whether they play as teams.</param>
/// <param name="DurationMinutes">The match timer's length.</param>
/// <param name="AllowOvertime">Whether the timer keeps counting up after it reaches zero.</param>
public record MatchSettings(
    int PointsToWin,
    MatchFormat Format,
    MatchMode Mode,
    int DurationMinutes,
    bool AllowOvertime);
