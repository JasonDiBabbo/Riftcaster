namespace Riftcaster.Admin.Layout;

/// <summary>
/// What the side navigation is showing, shared by the navigation itself and each page's header
/// (whose menu button opens it on a phone). Cascaded from <see cref="MainLayout"/>.
/// </summary>
/// <remarks>
/// The two states are separate because the screen decides which one applies. On a desktop the rail
/// is always on screen, and <see cref="Collapsed"/> shrinks it to icons. On a tablet, it is always
/// icons, and <see cref="MenuOpen"/> opens it over the page. On a phone, it is hidden, and
/// <see cref="MenuOpen"/> opens it as a full-screen menu. The stylesheet picks, so the server never
/// needs to know the screen's size.
/// </remarks>
public sealed class NavMenuState
{
    /// <summary>
    /// Whether the rail is shrunk to icons on a desktop.
    /// </summary>
    public bool Collapsed { get; private set; }

    /// <summary>
    /// Whether the menu is open over the page, on a tablet or phone.
    /// </summary>
    public bool MenuOpen { get; private set; }

    /// <summary>
    /// Raised when <see cref="Collapsed"/> or <see cref="MenuOpen"/> changes, for components to redraw.
    /// </summary>
    public event Action? Changed;

    public void ToggleCollapsed()
    {
        Collapsed = !Collapsed;
        Changed?.Invoke();
    }

    public void OpenMenu()
    {
        if (!MenuOpen)
        {
            MenuOpen = true;
            Changed?.Invoke();
        }
    }

    public void CloseMenu()
    {
        if (MenuOpen)
        {
            MenuOpen = false;
            Changed?.Invoke();
        }
    }
}
