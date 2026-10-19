using Bunit;
using Riftcaster.Admin.Layout;

namespace Riftcaster.Admin.Tests;

public class PageHeaderTests : BunitContext
{
    private readonly NavMenuState _nav = new();

    [Fact]
    public void ShowsTheTitleAndWhatThePageAdds()
    {
        var header = RenderHeader(parameters => parameters
            .Add(h => h.Title, "Live")
            .AddChildContent("<span class=\"extra\">On air</span>"));

        Assert.Equal("Live", header.Find("h1").TextContent);
        Assert.Equal("On air", header.Find(".status .extra").TextContent);
    }

    [Fact]
    public void Section_FollowsTheTitle()
    {
        var header = RenderHeader(parameters => parameters
            .Add(h => h.Title, "Live")
            .Add(h => h.Section, "Players"));

        Assert.Equal("Live · Players", header.Find("h1").TextContent);
        Assert.Equal(" · Players", header.Find("h1 .section").TextContent); // Wider screens hide just this part
    }

    [Fact]
    public void NoSection_LeavesTheTitleAlone()
    {
        var header = RenderHeader(parameters => parameters.Add(h => h.Title, "Settings"));

        Assert.Empty(header.FindAll(".section"));
    }

    [Fact]
    public void MenuButton_OpensTheMenu()
    {
        var header = RenderHeader(parameters => parameters.Add(h => h.Title, "Live"));
        Assert.Equal("false", header.Find(".menu-button").GetAttribute("aria-expanded"));

        header.Find(".menu-button").Click();

        Assert.True(_nav.MenuOpen);
        Assert.Equal("true", header.Find(".menu-button").GetAttribute("aria-expanded"));
    }

    private IRenderedComponent<PageHeader> RenderHeader(Action<ComponentParameterCollectionBuilder<PageHeader>> parameters) =>
        Render<PageHeader>(builder =>
        {
            builder.AddCascadingValue(_nav);
            parameters(builder);
        });
}
