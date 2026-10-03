namespace Riftcaster.Contracts;

/// <summary>
/// One player in the match: who they are, what they're playing, and their score.
/// </summary>
/// <remarks>
/// The legend, champion and battlefield are copies of the cards as they were when chosen, not
/// just their ids, so they still show after a restart while the card catalogue is loading, and
/// after a refresh changes them.
/// </remarks>
/// <param name="Name">The player's display name. Empty until the operator enters one.</param>
/// <param name="Legend">The player's legend, or <see langword="null"/> if none is chosen.</param>
/// <param name="Champion">The player's chosen champion, or <see langword="null"/> if none is chosen.</param>
/// <param name="Battlefield">The player's battlefield, or <see langword="null"/> if none is chosen.</param>
/// <param name="Points">Points in the current game. Not used in 2v2, where the team holds the points.</param>
/// <param name="GameWins">Games won in the match. Not used in 2v2, where the team holds the wins.</param>
/// <param name="Xp">The player's XP. Per player in every mode, including 2v2.</param>
public record Player(
    string Name,
    Card? Legend,
    Card? Champion,
    Card? Battlefield,
    int Points,
    int GameWins,
    int Xp);
