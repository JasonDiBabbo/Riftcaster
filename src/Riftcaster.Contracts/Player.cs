namespace Riftcaster.Contracts;

/// <summary>
/// One player in the match: who they are, what they're playing, and their score.
/// </summary>
/// <param name="Name">The player's display name. Empty until the operator enters one.</param>
/// <param name="Legend">The name of the player's legend, or <see langword="null"/> if none is chosen.</param>
/// <param name="Champion">The name of the player's chosen champion, or <see langword="null"/> if none is chosen.</param>
/// <param name="Battlefield">The name of the player's battlefield, or <see langword="null"/> if none is chosen.</param>
/// <param name="Points">Points in the current game. Not used in 2v2, where the team holds the points.</param>
/// <param name="GameWins">Games won in the match. Not used in 2v2, where the team holds the wins.</param>
/// <param name="Xp">The player's XP. Per player in every mode, including 2v2.</param>
public record Player(
    string Name,
    string? Legend,
    string? Champion,
    string? Battlefield,
    int Points,
    int GameWins,
    int Xp);
