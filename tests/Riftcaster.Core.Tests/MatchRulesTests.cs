using Riftcaster.Contracts;
using Riftcaster.Core.Match;

namespace Riftcaster.Core.Tests;

public class MatchRulesTests
{
    [Theory]
    [InlineData(MatchMode.OneVsOne, 2)]
    [InlineData(MatchMode.TwoVsTwo, 4)]
    [InlineData(MatchMode.FreeForAll3, 3)]
    [InlineData(MatchMode.FreeForAll4, 4)]
    public void PlayerCount_FollowsMode(MatchMode mode, int expected)
    {
        var settings = MatchRules.Default with { Mode = mode };

        Assert.Equal(expected, settings.PlayerCount);
    }

    [Theory]
    [InlineData(MatchFormat.BestOf1, 1)]
    [InlineData(MatchFormat.BestOf3, 2)]
    public void MaxGameWins_FollowsFormat(MatchFormat format, int expected)
    {
        var settings = MatchRules.Default with { Format = format };

        Assert.Equal(expected, settings.MaxGameWins);
    }

    [Fact]
    public void IsTeamMode_OnlyFor2v2()
    {
        Assert.True((MatchRules.Default with { Mode = MatchMode.TwoVsTwo }).IsTeamMode);
        Assert.False((MatchRules.Default with { Mode = MatchMode.FreeForAll4 }).IsTeamMode);
    }
}
