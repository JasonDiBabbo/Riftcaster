using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Riftcaster.Admin.Layout;

namespace Riftcaster.Admin.Tests;

public class SideNavTests : BunitContext
{
    private readonly NavMenuState _nav = new();

    private NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    [Fact]
    public void ListsEveryPageInItsGroup()
    {
        var sideNav = RenderNav();

        Assert.Equal(["Broadcast", "System"], sideNav.FindAll(".group-title").Select(title => title.TextContent));
        Assert.Equal(
            ["Live", "Match setup", "Overlays", "Network", "Settings", "Logs", "About"],
            sideNav.FindAll("a.item").Select(link => link.TextContent.Trim()));
        Assert.Equal("/settings", sideNav.FindAll("a.item").Single(link => link.TextContent.Trim() == "Settings").GetAttribute("href"));
    }

    [Fact]
    public void MarksThePageYouAreOn()
    {
        Navigation.NavigateTo("/settings");

        var sideNav = RenderNav();

        var current = sideNav.FindAll("a.item[aria-current]");
        Assert.Equal("Settings", Assert.Single(current).TextContent.Trim());
        Assert.Contains("active", current[0].ClassList);
    }

    [Fact]
    public void Dashboard_IsCurrentAtTheRoot()
    {
        var sideNav = RenderNav();

        Assert.Equal("Live", Assert.Single(sideNav.FindAll("a.item[aria-current]")).TextContent.Trim());
    }

    [Fact]
    public void Navigating_MovesTheMarkerAndClosesTheMenu()
    {
        var sideNav = RenderNav();
        _nav.OpenMenu();

        Navigation.NavigateTo("/logs");

        sideNav.WaitForAssertion(() =>
        {
            Assert.Equal("Logs", Assert.Single(sideNav.FindAll("a.item[aria-current]")).TextContent.Trim());
            Assert.False(_nav.MenuOpen);
        });
    }

    [Fact]
    public void ClickingThePageYouAreOn_ClosesTheMenu()
    {
        var sideNav = RenderNav();
        _nav.OpenMenu();

        sideNav.FindAll("a.item").Single(link => link.TextContent.Trim() == "Live").Click();

        Assert.False(_nav.MenuOpen);
    }

    [Fact]
    public void Filter_ShowsOnlyThePagesThatMatch()
    {
        var sideNav = RenderNav();

        sideNav.Find(".find input").Input("lo");

        Assert.Equal(["Logs"], sideNav.FindAll("a.item").Select(link => link.TextContent.Trim()));
        Assert.Equal(["System"], sideNav.FindAll(".group-title").Select(title => title.TextContent)); // An empty group is not shown
    }

    [Fact]
    public void Filter_WithNoMatch_SaysSo()
    {
        var sideNav = RenderNav();

        sideNav.Find(".find input").Input("zzz");

        Assert.Empty(sideNav.FindAll("a.item"));
        Assert.Equal("No pages match.", sideNav.Find(".none").TextContent);
    }

    [Fact]
    public void MenuOpen_ShowsTheOpenMenuAndAScrim()
    {
        var sideNav = RenderNav();
        Assert.DoesNotContain("open", sideNav.Find("nav").ClassList);
        Assert.Empty(sideNav.FindAll(".scrim.shown")); // It's always there, so it can fade; only visible while open

        sideNav.Find(".expand-toggle").Click();

        Assert.True(_nav.MenuOpen);
        Assert.Contains("open", sideNav.Find("nav").ClassList);
        Assert.Single(sideNav.FindAll(".scrim.shown"));
    }

    [Fact]
    public void ExpandToggle_BecomesCollapseWhileOpen_AndClosesTheMenu()
    {
        var sideNav = RenderNav();
        Assert.Equal("Expand navigation", sideNav.Find(".expand-toggle").GetAttribute("aria-label"));

        sideNav.Find(".expand-toggle").Click();

        Assert.Equal("Collapse navigation", sideNav.Find(".expand-toggle").GetAttribute("aria-label"));
        Assert.Equal("Collapse", sideNav.Find(".expand-toggle .label").TextContent);
        Assert.Equal("true", sideNav.Find(".expand-toggle").GetAttribute("aria-expanded"));

        sideNav.Find(".expand-toggle").Click();

        Assert.False(_nav.MenuOpen);
        Assert.Equal("Expand navigation", sideNav.Find(".expand-toggle").GetAttribute("aria-label"));
    }

    [Fact]
    public void ScrimAndCloseButtonAndEscape_CloseTheMenu()
    {
        var sideNav = RenderNav();

        _nav.OpenMenu();
        sideNav.Find(".scrim").Click();
        Assert.False(_nav.MenuOpen);

        _nav.OpenMenu();
        sideNav.Find(".close").Click();
        Assert.False(_nav.MenuOpen);

        _nav.OpenMenu();
        sideNav.Find("nav").KeyDown("Escape");
        Assert.False(_nav.MenuOpen);
    }

    [Fact]
    public void CollapseToggle_ShrinksTheRailAndExpandsItAgain()
    {
        var sideNav = RenderNav();
        Assert.DoesNotContain("collapsed", sideNav.Find(".slot").ClassList);
        Assert.Equal("Collapse navigation", sideNav.Find(".collapse-toggle").GetAttribute("aria-label"));

        sideNav.Find(".collapse-toggle").Click();

        Assert.Contains("collapsed", sideNav.Find(".slot").ClassList);
        Assert.Equal("Expand navigation", sideNav.Find(".collapse-toggle").GetAttribute("aria-label"));
        Assert.Equal("false", sideNav.Find(".collapse-toggle").GetAttribute("aria-expanded"));

        sideNav.Find(".collapse-toggle").Click();

        Assert.DoesNotContain("collapsed", sideNav.Find(".slot").ClassList);
    }

    private IRenderedComponent<SideNav> RenderNav() =>
        Render<SideNav>(parameters => parameters.AddCascadingValue(_nav));
}
