using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Riftcaster.Contracts;
using Riftcaster.Core.Match;

namespace Riftcaster.Core.Players;

/// <summary>
/// The service for the players' details and scores, and the teams' scores in 2v2.
/// </summary>
/// <remarks>
/// Scores are kept within the match's limits: points up to its points to win, and game wins up
/// to its maximum for the format. When the match settings change, scores above the new limits
/// are lowered to them.
/// </remarks>
public class PlayersService
{
    /// <summary>
    /// How many players the state holds: enough for the largest mode.
    /// </summary>
    public const int MaxPlayers = 4;

    /// <summary>
    /// How many teams the state holds (used in 2v2).
    /// </summary>
    public const int TeamCount = 2;

    /// <summary>
    /// The highest allowed XP.
    /// </summary>
    public const int MaxXp = 99;

    private static readonly Player NewPlayer = new(
        Name: "",
        Legend: null,
        Champion: null,
        Battlefield: null,
        Points: 0,
        GameWins: 0,
        Xp: 0);

    private static readonly TeamScore NewTeam = new(Points: 0, GameWins: 0);

    private readonly IPlayersStore _store;

    private readonly MatchService _match;

    private readonly Lock _lock = new();

    // The players together with the match settings they were last checked against, as one
    // reference, so readers always see a matching pair.
    private MatchState _current;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayersService"/> class.
    /// </summary>
    /// <param name="store">Loads and saves the players and teams.</param>
    /// <param name="match">The match settings, which set the score limits.</param>
    public PlayersService(IPlayersStore store, MatchService match)
    {
        _store = store;
        _match = match;
        _current = new MatchState(match.Settings, Normalize(store.Load() ?? new PlayersState([], []), match.Settings));

        // Lower any scores above the new limits, and pass the new settings on to watchers.
        // Both services live as long as the app, so this handler never needs removing.
        match.Changed += () => Apply(state => state);
    }

    /// <summary>
    /// The current players and teams.
    /// </summary>
    public PlayersState State => _current.Players;

    /// <summary>
    /// The current players and teams, with the match settings they were checked against.
    /// </summary>
    public MatchState MatchState => _current;

    /// <summary>
    /// Raised after the players, the teams or the match settings change.
    /// Handlers read <see cref="State"/> or <see cref="MatchState"/> for the new values.
    /// </summary>
    /// <remarks>
    /// May be raised on any thread.
    /// </remarks>
    public event Action? Changed;

    /// <summary>
    /// Changes one player, for example <c>UpdatePlayer(0, player => player with { Name = "Mara" })</c>.
    /// Scores outside the limits are clamped to them.
    /// </summary>
    /// <param name="index">The player's seat, from 0 to 3.</param>
    /// <param name="change">
    /// Builds the new player from the current one. Runs inside the lock, so keep it to a
    /// <c>with</c> expression: don't call back into this service from it.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not a seat.</exception>
    public void UpdatePlayer(int index, Func<Player, Player> change)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, MaxPlayers);
        ArgumentNullException.ThrowIfNull(change);

        Apply(state => state with { Players = Replace(state.Players, index, change) });
    }

    /// <summary>
    /// Changes one team's score, for example <c>UpdateTeam(1, team => team with { Points = 3 })</c>.
    /// Scores outside the limits are clamped to them.
    /// </summary>
    /// <param name="index">0 for Team A, 1 for Team B.</param>
    /// <param name="change">
    /// Builds the new score from the current one. Runs inside the lock, so keep it to a
    /// <c>with</c> expression: don't call back into this service from it.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not a team.</exception>
    public void UpdateTeam(int index, Func<TeamScore, TeamScore> change)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, TeamCount);
        ArgumentNullException.ThrowIfNull(change);

        Apply(state => state with { Teams = Replace(state.Teams, index, change) });
    }

    /// <summary>
    /// Swaps two players' seats. Each player's details and scores move with them.
    /// </summary>
    /// <remarks>
    /// The seat decides the side in 1v1 and the team in 2v2, so this is how a player changes
    /// side or team. Team scores stay with their team.
    /// </remarks>
    /// <param name="first">One player's seat, from 0 to 3.</param>
    /// <param name="second">The other player's seat, from 0 to 3.</param>
    /// <exception cref="ArgumentOutOfRangeException">A seat is out of range.</exception>
    public void SwapPlayers(int first, int second)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(first);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(first, MaxPlayers);
        ArgumentOutOfRangeException.ThrowIfNegative(second);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(second, MaxPlayers);

        Apply(state => state with
        {
            Players = [.. state.Players.Select((player, i) =>
                i == first ? state.Players[second] :
                i == second ? state.Players[first] :
                player)],
        });
    }

    /// <summary>
    /// Sets every player's points, game wins and XP, and every team's points and game wins, to 0.
    /// Names, legends, champions and battlefields are kept.
    /// </summary>
    public void ResetScores() => Apply(state => state with
    {
        Players = [.. state.Players.Select(player => player with { Points = 0, GameWins = 0, Xp = 0 })],
        Teams = [.. state.Teams.Select(team => team with { Points = 0, GameWins = 0 })],
    });

    /// <summary>
    /// Streams the players and match settings: first the current state, then each change.
    /// </summary>
    /// <remarks>
    /// If changes arrive faster than the caller reads them, only the latest is sent.
    /// </remarks>
    /// <param name="cancellationToken">Ends the stream, normally rather than with an exception.</param>
    /// <returns>The current state, then each new state.</returns>
    public async IAsyncEnumerable<MatchState> WatchAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // A signal only: the reader looks up the current state itself, so it always sends the latest.
        var changed = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });

        void OnChanged()
        {
            changed.Writer.TryWrite(true);
        }

        using var registration = cancellationToken.Register(() => changed.Writer.TryComplete());
        Changed += OnChanged;

        try
        {
            var last = _current;
            yield return last; // Initial state

            // Not cancellationToken: cancellation completes the channel (above), which ends this loop
            // normally. Passing the token would end it with an OperationCanceledException instead.
            await foreach (var _ in changed.Reader.ReadAllAsync(CancellationToken.None))
            {
                var current = _current;
                if (current == last)
                {
                    continue; // Already sent: it was read after an earlier signal
                }

                last = current;
                yield return current;
            }
        }
        finally
        {
            Changed -= OnChanged;
        }
    }

    private void Apply(Func<PlayersState, PlayersState> change)
    {
        lock (_lock)
        {
            var settings = _match.Settings;
            var updated = new MatchState(settings, Normalize(change(_current.Players), settings));
            if (updated == _current)
            {
                return;
            }

            var playersChanged = updated.Players != _current.Players;
            _current = updated;

            if (playersChanged)
            {
                _store.Save(updated.Players); // Inside the lock, so saves happen in the same order as the changes
            }
        }

        Changed?.Invoke();
    }

    private static IReadOnlyList<T> Replace<T>(IReadOnlyList<T> items, int index, Func<T, T> change) =>
        [.. items.Select((item, i) => i == index ? change(item) : item)];

    // Exactly four players and two teams (padding with empty ones, or dropping extras),
    // with every score inside the limits. A saved file might hold anything.
    private static PlayersState Normalize(PlayersState state, MatchSettings settings) => new(
        [.. Enumerable.Range(0, MaxPlayers).Select(i => Clamp(state.Players.ElementAtOrDefault(i) ?? NewPlayer, settings))],
        [.. Enumerable.Range(0, TeamCount).Select(i => Clamp(state.Teams.ElementAtOrDefault(i) ?? NewTeam, settings))]);

    private static Player Clamp(Player player, MatchSettings settings) => player with
    {
        Points = Math.Clamp(player.Points, 0, settings.PointsToWin),
        GameWins = Math.Clamp(player.GameWins, 0, settings.MaxGameWins),
        Xp = Math.Clamp(player.Xp, 0, MaxXp),
    };

    private static TeamScore Clamp(TeamScore team, MatchSettings settings) => team with
    {
        Points = Math.Clamp(team.Points, 0, settings.PointsToWin),
        GameWins = Math.Clamp(team.GameWins, 0, settings.MaxGameWins),
    };
}
