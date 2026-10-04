using Riftcaster.Admin.FeaturedCard;
using Riftcaster.Admin.Players;
using Riftcaster.Contracts;

namespace Riftcaster.Admin.Tests;

// The pure helpers behind the panels: what they show, worked out without rendering anything.
public class LabelTests
{
    [Theory]
    [InlineData(30, "just now")]
    [InlineData(5 * 60, "5 min ago")]
    [InlineData(3 * 3600, "3h ago")]
    [InlineData(30 * 3600, "1 day ago")]
    [InlineData(3 * 86400, "3 days ago")]
    public void CatalogStatusLabel_Ago(int seconds, string expected)
    {
        Assert.Equal(expected, CatalogStatusLabel.Ago(TimeSpan.FromSeconds(seconds)));
    }

    [Theory]
    [InlineData(CardType.Unit, CardSupertype.Champion, "Champion Unit")]
    [InlineData(CardType.Legend, null, "Legend")]
    [InlineData(CardType.Spell, CardSupertype.Other, "Spell")] // An unknown supertype isn't shown
    public void CardLabels_Type(CardType type, CardSupertype? supertype, string expected)
    {
        Assert.Equal(expected, CardLabels.Type(TestCards.Card("1", "Card", type, supertype)));
    }

    [Fact]
    public void CardLabels_Cost_DashWhenThereIsNone()
    {
        Assert.Equal("3", CardLabels.Cost(TestCards.Card("1", "Card", CardType.Unit, energy: 3)));
        Assert.Equal("—", CardLabels.Cost(TestCards.Card("1", "Card", CardType.Legend)));
    }

    private static readonly IReadOnlyList<Card> Champions =
    [
        TestCards.Champion("1", "Jinx, Demolitionist"),
        TestCards.Champion("2", "Jinx, Rebel"),
        TestCards.Champion("3", "Jinxy, Not Jinx"),
        TestCards.Champion("4", "Viktor, Leader"),
    ];

    [Fact]
    public void CardChoices_ChampionsFor_TheLegendsChampion()
    {
        var champions = CardChoices.ChampionsFor(TestCards.Legend("9", "Jinx, Loose Cannon"), Champions);

        Assert.Equal(["Jinx, Demolitionist", "Jinx, Rebel"], champions.Select(card => card.Name)); // Not "Jinxy"
    }

    [Fact]
    public void CardChoices_ChampionsFor_NoLegend_IsEveryChampion()
    {
        Assert.Same(Champions, CardChoices.ChampionsFor(null, Champions));
    }

    [Fact]
    public void CardChoices_ChampionsFor_NoMatches_IsEveryChampion()
    {
        // An unusual legend name never leaves the list empty.
        Assert.Same(Champions, CardChoices.ChampionsFor(TestCards.Legend("9", "Teemo, Swift Scout"), Champions));
    }
}
