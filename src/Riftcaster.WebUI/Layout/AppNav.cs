namespace Riftcaster.WebUI.Layout;

/// <summary>
/// One destination in the web interface's side navigation.
/// </summary>
/// <param name="Title">The label shown beside the icon.</param>
/// <param name="Href">The page's route, relative to the site root.</param>
/// <param name="Icon">
/// An SVG path (<c>d</c> attribute) drawn in a 24 by 24 box, stroked and not filled, like the rest of the set.
/// </param>
public sealed record NavItem(string Title, string Href, string Icon);

/// <summary>
/// A titled set of <see cref="NavItem"/>s.
/// </summary>
public sealed record NavGroup(string Title, IReadOnlyList<NavItem> Items);

/// <summary>
/// The web interface's pages, as the side navigation lists them.
/// </summary>
/// <remarks>
/// Adding a page to the navigation takes an entry here and a page with the same route: the side
/// navigation (including its phone menu and its filter) and the remote sign-in redirect both read
/// from this list.
/// </remarks>
public static class AppNav
{
    public const string LivePath = "/";

    public const string MatchSetupPath = "/match-setup";

    /// <summary>
    /// Not "/overlays": the overlay pages themselves are served there (and open to the network).
    /// </summary>
    public const string OverlaysPath = "/overlay-list";

    public const string NetworkPath = "/network";

    public const string SettingsPath = "/settings";

    public const string LogsPath = "/logs";

    public const string AboutPath = "/about";

    public static IReadOnlyList<NavGroup> Groups { get; } =
        [
            new("Broadcast",
            [
                new("Live", LivePath, "M3 5h18v11H3zM8 20h8M12 16v4"),
                new("Match setup", MatchSetupPath, "M4 7h10M18 7h2M4 17h2M10 17h10M16 5v4M8 15v4"),
                new("Overlays", OverlaysPath, "M3 5h14v10H3zM7 9h14v10H7z"),
                new("Network", NetworkPath, "M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18zM3 12h18M12 3c3 3 3 15 0 18M12 3c-3 3-3 15 0 18"),
            ]),
            new("System",
            [
                new("Settings", SettingsPath, "M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2zM15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0z"),
                new("Logs", LogsPath, "M6 3h9l4 4v14H6zM9 12h7M9 16h7"),
                new("About", AboutPath, "M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18zM12 11v6M12 7.5v.5"),
            ]),
        ];

    /// <summary>
    /// Whether this request path is one of the web interface's pages (as opposed to an overlay, the REST
    /// API, or a script or font the pages load).
    /// </summary>
    /// <param name="path">The request path, such as <c>/settings</c>.</param>
    public static bool IsAppPage(string? path)
    {
        var normalized = Normalize(path);
        return Groups.SelectMany(group => group.Items).Any(item => Normalize(item.Href) == normalized);
    }

    /// <summary>
    /// Whether <paramref name="item"/> is the page at <paramref name="path"/>.
    /// </summary>
    public static bool IsCurrent(NavItem item, string? path) =>
        Normalize(item.Href) == Normalize(path);

    // "/settings/" and "/Settings" are the same page as "/settings"; the site root is "/".
    private static string Normalize(string? path)
    {
        var trimmed = (path ?? "").TrimEnd('/');
        return trimmed.Length == 0 ? "/" : trimmed.ToLowerInvariant();
    }
}
