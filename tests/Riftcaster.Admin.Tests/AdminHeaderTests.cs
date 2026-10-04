using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Riftcaster.Admin.Layout;
using Riftcaster.Core.Cards;
using Riftcaster.Core.FeaturedCard;
using Riftcaster.Core.LowerThird;
using Riftcaster.Core.Match;
using Riftcaster.Core.Network;
using Riftcaster.Core.Overlays;

namespace Riftcaster.Admin.Tests;

public class AdminHeaderTests : BunitContext
{
    private readonly OverlayConnections _connections = new();

    private readonly AccessCode _accessCode = new(new InMemoryAccessCodeStore());

    public AdminHeaderTests()
    {
        var catalog = new CardCatalog(new InMemoryCardStore(), new InMemoryCardSource(), TimeProvider.System, NullLogger<CardCatalog>.Instance);
        Services.AddSingleton(new FeaturedCardService(new InMemoryFeaturedCardStore(), catalog));
        Services.AddSingleton(new LowerThirdService(new InMemoryLowerThirdStore()));
        Services.AddSingleton(new MatchService(new InMemoryMatchStore()));
        Services.AddSingleton(_connections);
        Services.AddSingleton(new NetworkAccess(false, [NetworkAccess.DefaultUrl]));
        Services.AddSingleton(_accessCode);
    }

    [Fact]
    public void ConnectionPill_CountsTheOverlays()
    {
        var header = RenderHeader(new OverlayFiles(@"C:\Riftcaster\overlays", true));
        Assert.Equal("No overlays connected", header.Find(".connection").TextContent);
        Assert.Contains("none", header.Find(".connection").ClassList); // A warning: OBS isn't connected yet

        using var first = _connections.Connect();
        header.WaitForAssertion(() => Assert.Equal("1 overlay connected", header.Find(".connection").TextContent));

        using var second = _connections.Connect();
        header.WaitForAssertion(() => Assert.Equal("2 overlays connected", header.Find(".connection").TextContent));
        Assert.DoesNotContain("none", header.Find(".connection").ClassList);
    }

    [Fact]
    public void NoOverlayFiles_ShowsAnErrorInPlaceOfTheCount()
    {
        var header = RenderHeader(new OverlayFiles(@"C:\Riftcaster\overlays", false));

        var pill = header.Find(".connection");
        Assert.Equal("Overlay files missing", pill.TextContent.Trim());
        Assert.Contains("missing", pill.ClassList);
        Assert.Equal("alert", pill.GetAttribute("role"));
    }

    private IRenderedComponent<AdminHeader> RenderHeader(OverlayFiles files)
    {
        Services.AddSingleton(files);
        return Render<AdminHeader>(parameters => parameters.AddCascadingValue(new OperatorSession(true, _accessCode.Version)));
    }
}
