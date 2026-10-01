using Riftcaster.Contracts;

namespace Riftcaster.Core.Players;

/// <summary>
/// An in-memory store for the players and teams.
/// </summary>
/// <remarks>
/// Nothing survives a server restart. For tests.
/// </remarks>
public sealed class InMemoryPlayersStore : IPlayersStore
{
    /// <summary>
    /// The most recently saved state.
    /// </summary>
    public PlayersState? State { get; private set; }

    /// <summary>
    /// How many times <see cref="Save"/> has been called, so tests can check when the service saves.
    /// </summary>
    public int SaveCount { get; private set; }

    /// <inheritdoc/>
    public PlayersState? Load() => State;

    /// <inheritdoc/>
    public void Save(PlayersState state)
    {
        State = state;
        SaveCount++;
    }
}
