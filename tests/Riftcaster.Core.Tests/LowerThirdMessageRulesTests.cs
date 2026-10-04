using Riftcaster.Contracts;
using Riftcaster.Core.LowerThird;

namespace Riftcaster.Core.Tests;

public class LowerThirdMessageRulesTests
{
    [Fact]
    public void Keyword_IsTrimmed()
    {
        var message = LowerThirdMessageRules.Normalize(new LowerThirdKeywordMessage(" Deflect ", "Costs more.\n"), out var errors);

        Assert.Empty(errors);
        Assert.Equal(new LowerThirdKeywordMessage("Deflect", "Costs more."), message);
    }

    [Fact]
    public void Keyword_NeedsBothFields()
    {
        LowerThirdMessageRules.Normalize(new LowerThirdKeywordMessage(null!, "  "), out var errors);

        Assert.Equal(["description", "keyword"], errors.Keys.Order());
    }

    [Fact]
    public void Information_NeedsText()
    {
        LowerThirdMessageRules.Normalize(new LowerThirdInformationMessage(""), out var errors);

        Assert.Equal(["Required."], errors["message"]);
    }

    [Fact]
    public void Socials_DropsLinksWithNoHandle_AndTrimsTheRest()
    {
        var message = LowerThirdMessageRules.Normalize(new LowerThirdSocialsMessage(
            [new(SocialNetwork.Twitch, " riftcaster "), new(SocialNetwork.X, ""), new(SocialNetwork.Discord, null!)]), out var errors);

        Assert.Empty(errors);
        Assert.Equal(new LowerThirdSocialsMessage([new(SocialNetwork.Twitch, "riftcaster")]), message);
    }

    [Fact]
    public void Socials_NeedsAHandle()
    {
        LowerThirdMessageRules.Normalize(new LowerThirdSocialsMessage([new(SocialNetwork.X, " ")]), out var errors);

        Assert.Equal(["Add at least one link with a handle."], errors["links"]);
    }

    [Fact]
    public void Socials_NoLinks_NeedsAHandle()
    {
        LowerThirdMessageRules.Normalize(new LowerThirdSocialsMessage(null!), out var errors);

        Assert.Contains("links", errors.Keys);
    }

    [Fact]
    public void Socials_BadLinks_AreNamed()
    {
        LowerThirdMessageRules.Normalize(new LowerThirdSocialsMessage([null!, new((SocialNetwork)42, "x")]), out var errors);

        Assert.Equal(["links[0]", "links[1].network"], errors.Keys.Order());
        Assert.DoesNotContain("links", errors.Keys); // Only the real problems, not "add a handle" too
    }

    [Fact]
    public void UnknownKind_Throws()
    {
        Assert.Throws<ArgumentException>(() => LowerThirdMessageRules.Normalize(new UnknownMessage(), out _));
    }

    private sealed record UnknownMessage : LowerThirdMessage;
}
