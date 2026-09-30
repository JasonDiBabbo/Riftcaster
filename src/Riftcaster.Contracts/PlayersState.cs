namespace Riftcaster.Contracts;

/// <summary>
/// Every player and team, whatever the match mode.
/// </summary>
/// <remarks>
/// All four players and both teams are always present, so a player's details survive a switch
/// to a smaller mode and back. The match mode decides which are in use: the first two, three
/// or four players, and the teams only in 2v2.
/// </remarks>
/// <param name="Players">The four players, in seat order. In 2v2, the first two are Team A and the last two are Team B.</param>
/// <param name="Teams">The two teams' scores: Team A, then Team B.</param>
public sealed record PlayersState(IReadOnlyList<Player> Players, IReadOnlyList<TeamScore> Teams)
{
    /// <summary>
    /// Compares the players and teams themselves, in order, rather than the lists that hold them.
    /// </summary>
    /// <remarks>
    /// A record's generated equality compares each property with ==, which for a list means
    /// "the same list object". See <see cref="LowerThirdSocialsMessage"/> for the same fix.
    /// </remarks>
    /// <param name="other">The state to compare with.</param>
    /// <returns>
    /// <see langword="true"/> if both hold equal players and equal teams in the same order
    /// and <see langword="false"/> otherwise.
    /// </returns>
    public bool Equals(PlayersState? other) =>
        other is not null && Players.SequenceEqual(other.Players) && Teams.SequenceEqual(other.Teams);

    /// <summary>
    /// Hashes the players and teams, to match <see cref="Equals(PlayersState?)"/>:
    /// equal objects must have equal hash codes.
    /// </summary>
    /// <returns>A hash code.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var player in Players)
        {
            hash.Add(player);
        }

        foreach (var team in Teams)
        {
            hash.Add(team);
        }

        return hash.ToHashCode();
    }
}
