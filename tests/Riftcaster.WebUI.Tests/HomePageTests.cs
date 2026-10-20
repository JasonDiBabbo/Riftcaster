using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Riftcaster.WebUI.Layout;
using Riftcaster.WebUI.Pages;
using Riftcaster.Contracts;
using Riftcaster.Core.Cards;
using Riftcaster.Core.FeaturedCard;
using Riftcaster.Core.LowerThird;
using Riftcaster.Core.Match;
using Riftcaster.Core.Network;
using Riftcaster.Core.Overlays;
using Riftcaster.Core.Players;
using Riftcaster.Core.Timer;

namespace Riftcaster.WebUI.Tests;

// The Live page: its four sections, and on a phone the tab bar that shows one at a time.
public sealed class HomePageTests : BunitContext
{
    private readonly MatchService _match = new(new InMemoryMatchStore());

    private readonly TimerService _timer;

    private readonly AccessCode _accessCode = new(new InMemoryAccessCodeStore());

    public HomePageTests()
    {
        var catalog = new CardCatalog(new InMemoryCardStore(), new InMemoryCardSource(), TimeProvider.System, NullLogger<CardCatalog>.Instance);
        _timer = new TimerService(_match, new FakeTimeProvider());
        Services.AddSingleton(catalog);
        Services.AddSingleton(_match);
        Services.AddSingleton(new PlayersService(new InMemoryPlayersStore(), _match));
        Services.AddSingleton(_timer);
        Services.AddSingleton(new FeaturedCardService(new InMemoryFeaturedCardStore(), catalog));
        Services.AddSingleton(new LowerThirdService(new InMemoryLowerThirdStore()));
        Services.AddSingleton(new OverlayConnections());
        Services.AddSingleton(new OverlayFiles(@"C:\Riftcaster\overlays", true));
        Services.AddSingleton(new NetworkAccess(false, [NetworkAccess.DefaultUrl]));
        Services.AddSingleton(_accessCode);
        Services.AddSingleton(TimeProvider.System);
    }

    protected override void Dispose(bool disposing)
    {
        _timer.Dispose();
        base.Dispose(disposing);
    }

    [Fact]
    public void ShowsEverySection()
    {
        var page = RenderPage();

        Assert.Equal("Live", page.Find(".title").FirstChild!.TextContent);
        Assert.Single(page.FindAll(".tiles-slot"));
        Assert.Equal(["Players", "Timer", "Featured card", "Lower thirds"], page.FindAll(".slot .title").Select(title => title.TextContent));
    }

    [Fact]
    public void PlayerCount_IsOnTheGrid()
    {
        _match.Update(settings => settings with { Mode = MatchMode.FreeForAll3 });

        var page = RenderPage();

        Assert.Equal("3", page.Find(".grid").GetAttribute("data-players"));

        _match.Update(settings => settings with { Mode = MatchMode.OneVsOne });

        page.WaitForAssertion(() => Assert.Equal("2", page.Find(".grid").GetAttribute("data-players")));
    }

    [Theory]
    [InlineData("Timer", "Timer")]
    [InlineData("Cards", "Cards")]
    [InlineData("Lowers", "Lower thirds")]
    [InlineData("Players", "Players")]
    public void PhoneTabs_ChooseTheSectionAndNameItInTheHeader(string tab, string section)
    {
        var page = RenderPage();

        page.FindAll(".tab").Single(button => button.TextContent.Trim() == tab).Click();

        Assert.Equal(section, page.Find(".section").TextContent.Trim('·', ' '));
        Assert.Equal(section == "Lower thirds" ? "LowerThirds" : section, page.Find(".grid").GetAttribute("data-tab"));
        Assert.Contains("active", page.FindAll(".tab").Single(button => button.TextContent.Trim() == tab).ClassList);
    }

    private IRenderedComponent<Home> RenderPage() =>
        Render<Home>(parameters => parameters
            .AddCascadingValue(new NavMenuState())
            .AddCascadingValue(new OperatorSession(true, _accessCode.Version)));
}
