using Riftcaster.WebUI.Layout;

namespace Riftcaster.WebUI.Tests;

public class AppNavTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("")] // An empty path is the site root
    [InlineData(null)]
    [InlineData("/overlay-list")]
    [InlineData("/network")]
    [InlineData("/settings")]
    [InlineData("/Settings/")] // Case and a trailing slash don't change the page
    [InlineData("/logs")]
    [InlineData("/about")]
    public void IsAppPage_AnAppPage_IsTrue(string? path)
    {
        Assert.True(AppNav.IsAppPage(path));
    }

    [Theory]
    [InlineData("/overlays")] // The overlay pages, which are served there
    [InlineData("/overlays/timer/timer.html")]
    [InlineData("/api/timer")]
    [InlineData("/_blazor/negotiate")]
    [InlineData("/signin")]
    [InlineData("/settings/extra")]
    [InlineData("/nowhere")]
    public void IsAppPage_Anything_Else_IsFalse(string? path)
    {
        Assert.False(AppNav.IsAppPage(path));
    }

    [Fact]
    public void Pages_HaveUniqueRoutesAndTitles()
    {
        var items = AppNav.Groups.SelectMany(group => group.Items).ToList();

        Assert.Equal(items.Count, items.Select(item => item.Href.ToLowerInvariant()).Distinct().Count());
        Assert.Equal(items.Count, items.Select(item => item.Title).Distinct().Count());
    }

    [Fact]
    public void Pages_StayClearOfTheOverlayPages()
    {
        // /overlays is the overlay files' route, open to the network without a sign-in.
        Assert.DoesNotContain(AppNav.Groups.SelectMany(group => group.Items), item => item.Href.StartsWith("/overlays", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("/", "Live", true)]
    [InlineData("/", "Settings", false)]
    [InlineData("/settings", "Settings", true)]
    [InlineData("/settings/", "Settings", true)]
    [InlineData("/match-setup", "Match setup", true)]
    [InlineData("/match-setup", "Live", false)]
    [InlineData("/settings", "Live", false)]
    public void IsCurrent_MatchesTheItemsPage(string path, string title, bool expected)
    {
        var item = AppNav.Groups.SelectMany(group => group.Items).Single(item => item.Title == title);

        Assert.Equal(expected, AppNav.IsCurrent(item, path));
    }
}
