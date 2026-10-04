using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Riftcaster.Admin.FeaturedCard;
using Riftcaster.Contracts;
using Riftcaster.Core.Cards;
using Riftcaster.Core.FeaturedCard;

namespace Riftcaster.Admin.Tests;

public class FeaturedCardPanelTests : BunitContext
{
    private static readonly Card Jinx = TestCards.Legend("1", "Jinx, Loose Cannon");
    private static readonly Card JinxMetal = TestCards.Legend("2", "Jinx, Loose Cannon", variant: "Metal");
    private static readonly Card Poppy = TestCards.Champion("3", "Poppy, Paragon");

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));

    private readonly InMemoryCardStore _cardStore = new();

    private readonly InMemoryFeaturedCardStore _featuredStore = new();

    [Fact]
    public void Meta_ShowsTheCardCountAndAge()
    {
        var panel = RenderPanel(saved: [Jinx, Poppy], fetchedAgo: TimeSpan.FromHours(3));

        Assert.Equal("2 cards · updated 3h ago", panel.Find(".catalog-status").TextContent);
    }

    [Fact]
    public void Meta_KeepsTheAgeCurrent()
    {
        var panel = RenderPanel(saved: [Jinx], fetchedAgo: TimeSpan.FromMinutes(59));

        _time.Advance(TimeSpan.FromMinutes(2)); // The panel redraws each minute

        panel.WaitForAssertion(() => Assert.Equal("1 cards · updated 1h ago", panel.Find(".catalog-status").TextContent));
    }

    [Fact]
    public void NoCards_SaysTheCatalogueIsLoading()
    {
        var panel = RenderPanel(saved: null);

        Assert.Contains("Fetching the card catalogue", panel.Find(".results").TextContent);
    }

    [Fact]
    public void Search_ListsMatchesWithTheirCount()
    {
        var panel = RenderPanel(saved: [Jinx, JinxMetal, Poppy]);

        panel.Find("input[type=search]").Input("jinx");

        Assert.Equal("2 results", panel.Find(".meta").TextContent.Trim());
        Assert.Equal(
            ["Legend · Origins", "Legend · Metal · Origins"], // The set and variant tell the printings apart
            panel.FindAll(".result .meta").Select(meta => meta.TextContent));
    }

    [Fact]
    public void Search_NoMatches_SaysSo()
    {
        var panel = RenderPanel(saved: [Jinx]);

        panel.Find("input[type=search]").Input("zzz");

        Assert.Contains("No cards match", panel.Find(".results").TextContent);
    }

    [Fact]
    public void Search_MoreThanTheLimit_SaysSo()
    {
        var many = Enumerable.Range(1, 60).Select(i => TestCards.Legend($"{i}", $"Card {i}")).ToList();
        var panel = RenderPanel(saved: many);

        panel.Find("input[type=search]").Input("card");

        Assert.Equal("50+ results", panel.Find(".meta").TextContent.Trim());
        Assert.Equal(50, panel.FindAll(".result").Count);
    }

    [Fact]
    public void ClickingAResult_FeaturesItAndClearsTheSearch()
    {
        var panel = RenderPanel(saved: [Jinx, Poppy]);
        panel.Find("input[type=search]").Input("poppy");

        panel.Find(".result").Click();

        Assert.Equal(Poppy, _featuredStore.State?.Card);
        Assert.Equal("", panel.Find("input[type=search]").GetAttribute("value"));
        Assert.Equal("Poppy, Paragon", panel.Find(".featured-name").TextContent);
    }

    [Fact]
    public void Clear_TakesTheCardOffAir()
    {
        _featuredStore.Save(new FeaturedCardState(Jinx));
        var panel = RenderPanel(saved: [Jinx]);

        panel.FindAll(".featured button").Single(button => button.TextContent == "Clear").Click();

        Assert.Null(_featuredStore.State?.Card);
        Assert.Contains("No featured card", panel.Find(".featured").TextContent);
    }

    private IRenderedComponent<FeaturedCardPanel> RenderPanel(IReadOnlyList<Card>? saved, TimeSpan? fetchedAgo = null)
    {
        if (saved is not null)
        {
            _cardStore.Save(new CardCatalogSnapshot(saved, _time.GetUtcNow() - (fetchedAgo ?? TimeSpan.Zero)));
        }

        var catalog = new CardCatalog(_cardStore, new InMemoryCardSource(), _time, NullLogger<CardCatalog>.Instance);
        Services.AddSingleton<TimeProvider>(_time);
        Services.AddSingleton(catalog);
        Services.AddSingleton(new FeaturedCardService(_featuredStore, catalog));

        return Render<FeaturedCardPanel>();
    }
}
