using Riftcaster.Admin.Layout;

namespace Riftcaster.Admin.Tests;

public class AdminNavTests
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
    public void IsAdminPage_AnAdminPage_IsTrue(string? path)
    {
        Assert.True(AdminNav.IsAdminPage(path));
    }

    [Theory]
    [InlineData("/overlays")] // The overlay pages, which are served there
    [InlineData("/overlays/timer/timer.html")]
    [InlineData("/api/timer")]
    [InlineData("/_blazor/negotiate")]
    [InlineData("/signin")]
    [InlineData("/settings/extra")]
    [InlineData("/nowhere")]
    public void IsAdminPage_Anything_Else_IsFalse(string? path)
    {
        Assert.False(AdminNav.IsAdminPage(path));
    }

    [Fact]
    public void Pages_HaveUniqueRoutesAndTitles()
    {
        var items = AdminNav.Groups.SelectMany(group => group.Items).ToList();

        Assert.Equal(items.Count, items.Select(item => item.Href.ToLowerInvariant()).Distinct().Count());
        Assert.Equal(items.Count, items.Select(item => item.Title).Distinct().Count());
    }

    [Fact]
    public void Pages_StayClearOfTheOverlayPages()
    {
        // /overlays is the overlay files' route, open to the network without a sign-in.
        Assert.DoesNotContain(AdminNav.Groups.SelectMany(group => group.Items), item => item.Href.StartsWith("/overlays", StringComparison.OrdinalIgnoreCase));
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
        var item = AdminNav.Groups.SelectMany(group => group.Items).Single(item => item.Title == title);

        Assert.Equal(expected, AdminNav.IsCurrent(item, path));
    }
}
