using System.Text.Json;
using Riftcaster.Contracts;

namespace Riftcaster.Core.Tests;

public class LowerThirdSocialsMessageTests
{
    [Fact]
    public void Equals_SameLinksInDifferentLists_AreEqual()
    {
        var first = new LowerThirdSocialsMessage([new(SocialNetwork.Twitch, "Riftcaster"), new(SocialNetwork.X, "@Riftcaster")]);
        var second = new LowerThirdSocialsMessage([new(SocialNetwork.Twitch, "Riftcaster"), new(SocialNetwork.X, "@Riftcaster")]);
        Assert.NotSame(first.Links, second.Links);

        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Equals_SameLinksInDifferentOrder_AreNotEqual()
    {
        var first = new LowerThirdSocialsMessage([new(SocialNetwork.Twitch, "Riftcaster"), new(SocialNetwork.X, "@Riftcaster")]);
        var second = new LowerThirdSocialsMessage([new(SocialNetwork.X, "@Riftcaster"), new(SocialNetwork.Twitch, "Riftcaster")]);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Serialize_WritesTypeAndNetworkAsStrings()
    {
        LowerThirdMessage message = new LowerThirdSocialsMessage([new(SocialNetwork.YouTube, "@Riftcaster")]);

        var json = JsonSerializer.Serialize(message, JsonSerializerOptions.Web);

        Assert.Equal("""{"type":"socials","links":[{"network":"YouTube","handle":"@Riftcaster"}]}""", json);
        Assert.Equal(message, JsonSerializer.Deserialize<LowerThirdMessage>(json, JsonSerializerOptions.Web));
    }
}
