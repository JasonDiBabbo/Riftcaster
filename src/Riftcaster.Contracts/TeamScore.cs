namespace Riftcaster.Contracts;

/// <summary>
/// A team's score in 2v2, where points and game wins belong to the team rather than to its players.
/// </summary>
/// <param name="Points">Points in the current game.</param>
/// <param name="GameWins">Games won in the match.</param>
public record TeamScore(int Points, int GameWins);
