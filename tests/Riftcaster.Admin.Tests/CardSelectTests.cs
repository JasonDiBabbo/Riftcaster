using Bunit;
using Riftcaster.Admin.Players;
using Riftcaster.Contracts;

namespace Riftcaster.Admin.Tests;

public class CardSelectTests : BunitContext
{
    private static readonly Card Jinx = TestCards.Legend("1", "Jinx, Loose Cannon");
    private static readonly Card Viktor = TestCards.Legend("2", "Viktor, Herald of the Arcane");

    private bool _changed;

    private Card? _changedTo;

    [Fact]
    public void ListsNoneThenEachCard()
    {
        var select = RenderSelect([Jinx, Viktor], value: null);

        Assert.Equal(["None", "Jinx, Loose Cannon", "Viktor, Herald of the Arcane"], select.FindAll("option").Select(option => option.TextContent));
        Assert.Equal("", select.Find("select").GetAttribute("value"));
    }

    [Fact]
    public void SelectedCard_IsTheValue()
    {
        var select = RenderSelect([Jinx, Viktor], value: Viktor);

        Assert.Equal("2", select.Find("select").GetAttribute("value"));
    }

    [Fact]
    public void Choosing_PassesTheCard()
    {
        var select = RenderSelect([Jinx, Viktor], value: null);

        select.Find("select").Change("2");

        Assert.Equal(Viktor, _changedTo);
    }

    [Fact]
    public void ChoosingNone_PassesNull()
    {
        var select = RenderSelect([Jinx, Viktor], value: Jinx);

        select.Find("select").Change("");

        Assert.True(_changed);
        Assert.Null(_changedTo);
    }

    [Fact]
    public void SavedCardNotInTheList_IsStillShown()
    {
        // E.g. a saved choice while the catalogue is loading, or the legend changed under the champion.
        var select = RenderSelect([Jinx], value: Viktor);

        Assert.Contains("Viktor, Herald of the Arcane", select.FindAll("option").Select(option => option.TextContent));
        Assert.Equal("2", select.Find("select").GetAttribute("value"));
    }

    [Fact]
    public void NoCards_IsDisabledAndSaysLoading()
    {
        var select = RenderSelect([], value: null);

        Assert.True(select.Find("select").HasAttribute("disabled"));
        Assert.Equal("Loading cards…", select.Find("option").TextContent);
    }

    private IRenderedComponent<CardSelect> RenderSelect(IReadOnlyList<Card> options, Card? value) =>
        Render<CardSelect>(parameters => parameters
            .Add(select => select.Options, options)
            .Add(select => select.Value, value)
            .Add(select => select.ValueChanged, (Card? card) =>
            {
                _changed = true;
                _changedTo = card;
            }));
}
