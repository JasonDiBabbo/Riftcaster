using Riftcaster.Contracts;
using Riftcaster.Core.Match;
using Riftcaster.Core.Players;

namespace Riftcaster.Core.Tests;

public class PlayersServiceTests
{
    private static readonly Card Jinx = new("1", "ogn-251-298", "Jinx, Loose Cannon", null, CardType.Legend, null, "Fury / Chaos", null, "Origins", "https://cards.test/1.png", Landscape: false);

    private readonly InMemoryPlayersStore _store = new();

    private readonly MatchService _match = new(new InMemoryMatchStore()); // 8 points to win, best of 3

    private readonly PlayersService _service;

    private int _changedCount;

    public PlayersServiceTests()
    {
        _service = new PlayersService(_store, _match);
        _service.Changed += () => _changedCount++;
    }

    [Fact]
    public void Constructor_NothingSaved_StartsWithFourEmptyPlayersAndTwoTeams()
    {
        Assert.Equal(PlayersService.MaxPlayers, _service.State.Players.Count);
        Assert.All(_service.State.Players, player => Assert.Equal(new Player("", null, null, null, 0, 0, 0), player));
        Assert.Equal([new TeamScore(0, 0), new TeamScore(0, 0)], _service.State.Teams);
    }

    [Fact]
    public void Constructor_LoadsSavedState()
    {
        var saved = new PlayersState(
            [NewPlayer("Mara") with { Points = 5 }, NewPlayer("Dex"), NewPlayer("Juno"), NewPlayer("Theo")],
            [new TeamScore(3, 1), new TeamScore(0, 0)]);
        _store.Save(saved);

        var service = new PlayersService(_store, _match);

        Assert.Equal(saved, service.State);
    }

    [Fact]
    public void Constructor_SavedStateTooShort_PadsWithEmptyPlayersAndTeams()
    {
        _store.Save(new PlayersState([NewPlayer("Mara"), NewPlayer("Dex")], []));

        var service = new PlayersService(_store, _match);

        Assert.Equal(["Mara", "Dex", "", ""], service.State.Players.Select(player => player.Name));
        Assert.Equal(PlayersService.TeamCount, service.State.Teams.Count);
    }

    [Fact]
    public void Constructor_SavedStateTooLong_DropsExtras()
    {
        _store.Save(new PlayersState(
            [NewPlayer("A"), NewPlayer("B"), NewPlayer("C"), NewPlayer("D"), NewPlayer("E")],
            [new TeamScore(0, 0), new TeamScore(0, 0), new TeamScore(0, 0)]));

        var service = new PlayersService(_store, _match);

        Assert.Equal(["A", "B", "C", "D"], service.State.Players.Select(player => player.Name));
        Assert.Equal(PlayersService.TeamCount, service.State.Teams.Count);
    }

    [Fact]
    public void Constructor_ClampsSavedScores()
    {
        _store.Save(new PlayersState(
            [NewPlayer("Mara") with { Points = 99, GameWins = 5, Xp = 5000 }],
            [new TeamScore(-3, -1)]));

        var service = new PlayersService(_store, _match);

        Assert.Equal(NewPlayer("Mara") with { Points = 8, GameWins = 2, Xp = PlayersService.MaxXp }, service.State.Players[0]);
        Assert.Equal(new TeamScore(0, 0), service.State.Teams[0]);
    }

    [Fact]
    public void UpdatePlayer_ChangesOnlyThatPlayer()
    {
        _service.UpdatePlayer(1, player => player with { Name = "Dex", Legend = Jinx });

        Assert.Equal(NewPlayer("Dex") with { Legend = Jinx }, _service.State.Players[1]);
        Assert.Equal(["", "Dex", "", ""], _service.State.Players.Select(player => player.Name));
    }

    [Fact]
    public void UpdatePlayer_SavesAndRaisesChangedOnce()
    {
        _service.UpdatePlayer(0, player => player with { Points = 3 });

        Assert.Equal(_service.State, _store.State);
        Assert.Equal(1, _store.SaveCount);
        Assert.Equal(1, _changedCount);
    }

    [Fact]
    public void UpdatePlayer_SameContent_DoesNotSaveOrRaiseChanged()
    {
        _service.UpdatePlayer(0, player => player with { Name = "" });

        Assert.Equal(0, _store.SaveCount);
        Assert.Equal(0, _changedCount);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(PlayersService.MaxPlayers)]
    public void UpdatePlayer_NotASeat_Throws(int index)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _service.UpdatePlayer(index, player => player));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(5, 5)]
    [InlineData(9, 8)]
    public void UpdatePlayer_ClampsPointsToPointsToWin(int points, int expected)
    {
        _service.UpdatePlayer(0, player => player with { Points = points });

        Assert.Equal(expected, _service.State.Players[0].Points);
    }

    [Theory]
    [InlineData(MatchFormat.BestOf3, 2)]
    [InlineData(MatchFormat.BestOf1, 1)]
    public void UpdatePlayer_ClampsGameWinsToFormatMaximum(MatchFormat format, int expected)
    {
        _match.Update(settings => settings with { Format = format });

        _service.UpdatePlayer(0, player => player with { GameWins = 3 });

        Assert.Equal(expected, _service.State.Players[0].GameWins);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(1000, PlayersService.MaxXp)]
    public void UpdatePlayer_ClampsXp(int xp, int expected)
    {
        _service.UpdatePlayer(0, player => player with { Xp = xp });

        Assert.Equal(expected, _service.State.Players[0].Xp);
    }

    [Fact]
    public void UpdateTeam_ChangesOnlyThatTeam()
    {
        _service.UpdateTeam(1, team => team with { Points = 3, GameWins = 1 });

        Assert.Equal([new TeamScore(0, 0), new TeamScore(3, 1)], _service.State.Teams);
        Assert.Equal(1, _store.SaveCount);
        Assert.Equal(1, _changedCount);
    }

    [Fact]
    public void UpdateTeam_ClampsScores()
    {
        _service.UpdateTeam(0, team => team with { Points = 9, GameWins = -1 });

        Assert.Equal(new TeamScore(8, 0), _service.State.Teams[0]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(PlayersService.TeamCount)]
    public void UpdateTeam_NotATeam_Throws(int index)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _service.UpdateTeam(index, team => team));
    }

    [Fact]
    public void SwapPlayers_MovesDetailsAndScoresBetweenSeats()
    {
        _service.UpdatePlayer(1, player => player with { Name = "Dex", Points = 3, Xp = 9 });
        _service.UpdatePlayer(2, player => player with { Name = "Juno", Legend = Jinx });
        _changedCount = 0;

        _service.SwapPlayers(1, 2);

        Assert.Equal(NewPlayer("Juno") with { Legend = Jinx }, _service.State.Players[1]);
        Assert.Equal(NewPlayer("Dex") with { Points = 3, Xp = 9 }, _service.State.Players[2]);
        Assert.Equal(1, _changedCount);
    }

    [Fact]
    public void SwapPlayers_KeepsTeamScoresWithTheirTeams()
    {
        _service.UpdateTeam(0, team => team with { Points = 5 });

        _service.SwapPlayers(1, 2);

        Assert.Equal([new TeamScore(5, 0), new TeamScore(0, 0)], _service.State.Teams);
    }

    [Fact]
    public void SwapPlayers_SameSeat_DoesNotRaiseChanged()
    {
        _service.UpdatePlayer(0, player => player with { Name = "Mara" });
        _changedCount = 0;

        _service.SwapPlayers(0, 0);

        Assert.Equal(0, _changedCount);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, PlayersService.MaxPlayers)]
    public void SwapPlayers_NotASeat_Throws(int first, int second)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _service.SwapPlayers(first, second));
    }

    [Fact]
    public void ResetScores_ZeroesScoresAndKeepsDetails()
    {
        _service.UpdatePlayer(0, player => player with { Name = "Mara", Legend = Jinx, Points = 5, GameWins = 1, Xp = 12 });
        _service.UpdateTeam(1, team => team with { Points = 3, GameWins = 1 });
        _changedCount = 0;

        _service.ResetScores();

        Assert.Equal(NewPlayer("Mara") with { Legend = Jinx }, _service.State.Players[0]);
        Assert.Equal([new TeamScore(0, 0), new TeamScore(0, 0)], _service.State.Teams);
        Assert.Equal(1, _changedCount);
    }

    [Fact]
    public void MatchChanged_LowerLimits_LowersScoresAboveThem()
    {
        _match.Update(settings => settings with { PointsToWin = 12 });
        _service.UpdatePlayer(0, player => player with { Points = 10, GameWins = 2 });
        _service.UpdatePlayer(1, player => player with { Points = 6 });
        _service.UpdateTeam(0, team => team with { Points = 11, GameWins = 2 });
        _changedCount = 0;

        _match.Update(settings => settings with { PointsToWin = 8, Format = MatchFormat.BestOf1 });

        Assert.Equal((8, 1), (_service.State.Players[0].Points, _service.State.Players[0].GameWins));
        Assert.Equal(6, _service.State.Players[1].Points);
        Assert.Equal(new TeamScore(8, 1), _service.State.Teams[0]);
        Assert.Equal(_service.State, _store.State);
        Assert.Equal(1, _changedCount);
    }

    [Fact]
    public void MatchChanged_ScoresWithinLimits_RaisesChangedWithoutSaving()
    {
        _service.UpdatePlayer(0, player => player with { Points = 5 });
        var saveCount = _store.SaveCount;
        _changedCount = 0;

        _match.Update(settings => settings with { Mode = MatchMode.TwoVsTwo });

        Assert.Equal(MatchMode.TwoVsTwo, _service.MatchState.Settings.Mode);
        Assert.Equal(saveCount, _store.SaveCount);
        Assert.Equal(1, _changedCount);
    }

    [Fact]
    public async Task WatchAsync_YieldsCurrentStateFirst()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var watch = _service.WatchAsync(cts.Token).GetAsyncEnumerator();

        Assert.True(await watch.MoveNextAsync());
        Assert.Equal(new MatchState(_match.Settings, _service.State), watch.Current);
    }

    [Fact]
    public async Task WatchAsync_YieldsPlayerChanges()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var watch = _service.WatchAsync(cts.Token).GetAsyncEnumerator();
        Assert.True(await watch.MoveNextAsync()); // Consume the initial state

        _service.UpdatePlayer(0, player => player with { Name = "Mara" });

        Assert.True(await watch.MoveNextAsync());
        Assert.Equal("Mara", watch.Current.Players.Players[0].Name);
    }

    [Fact]
    public async Task WatchAsync_YieldsMatchChanges()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var watch = _service.WatchAsync(cts.Token).GetAsyncEnumerator();
        Assert.True(await watch.MoveNextAsync()); // Consume the initial state

        _match.Update(settings => settings with { Mode = MatchMode.FreeForAll3 });

        Assert.True(await watch.MoveNextAsync());
        Assert.Equal(MatchMode.FreeForAll3, watch.Current.Settings.Mode);
    }

    [Fact]
    public async Task WatchAsync_LowerLimits_YieldsSettingsAndLoweredScoresTogether()
    {
        _match.Update(settings => settings with { PointsToWin = 12 });
        _service.UpdatePlayer(0, player => player with { Points = 10 });
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var watch = _service.WatchAsync(cts.Token).GetAsyncEnumerator();
        Assert.True(await watch.MoveNextAsync()); // Consume the initial state
        var next = watch.MoveNextAsync(); // Wait for the next event before changing anything

        _match.Update(settings => settings with { PointsToWin = 8 });

        Assert.True(await next);
        Assert.Equal(8, watch.Current.Settings.PointsToWin);
        Assert.Equal(8, watch.Current.Players.Players[0].Points); // Never 10 against 8
    }

    [Fact]
    public async Task WatchAsync_EndsWhenCancelled()
    {
        using var cts = new CancellationTokenSource(); // no timeout: we cancel it ourselves
        await using var watch = _service.WatchAsync(cts.Token).GetAsyncEnumerator();
        Assert.True(await watch.MoveNextAsync());

        cts.Cancel();

        Assert.False(await watch.MoveNextAsync()); // Stream ended normally
    }

    private static Player NewPlayer(string name) => new(name, null, null, null, 0, 0, 0);
}
