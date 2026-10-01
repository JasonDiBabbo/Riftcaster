using Riftcaster.Contracts;

namespace Riftcaster.Core.Players;

/// <summary>
/// Loads and saves the players and teams, so they survive a server restart.
/// </summary>
public interface IPlayersStore
{
    /// <summary>
    /// Loads the saved players and teams.
    /// </summary>
    /// <returns>
    /// The saved state, or <see langword="null"/> if there is none (the service then starts with empty players).
    /// </returns>
    PlayersState? Load();

    /// <summary>
    /// Saves the players and teams, replacing any previous save.
    /// </summary>
    /// <remarks>
    /// Implementations should log failures rather than throw exceptions.
    /// </remarks>
    void Save(PlayersState state);
}
