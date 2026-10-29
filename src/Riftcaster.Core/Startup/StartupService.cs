using System.Security;
using Microsoft.Extensions.Logging;
using Riftcaster.Core.Settings;

namespace Riftcaster.Core.Startup;

/// <summary>
/// What the Settings page's startup section knows about starting Riftcaster: whether it starts with Windows,
/// and whether it opens the web interface when it does start.
/// </summary>
/// <remarks>
/// Starting with Windows belongs to the Windows app, which attaches its entry (<see cref="Attach"/>) once it
/// knows it's installed. Without that (the console server, or running from source), there is nothing to
/// start, and <see cref="Availability"/> says why. Opening the web interface is a saved setting, read by the
/// Windows app when it starts.
/// </remarks>
/// <param name="settings">Where opening the web interface is chosen.</param>
/// <param name="availability">Why there's no entry to change, until the Windows app attaches one.</param>
/// <param name="logger">Logs a change that Windows refused.</param>
public sealed class StartupService(AppSettingsService settings, StartupAvailability availability, ILogger<StartupService> logger)
{
    private readonly Lock _lock = new();

    private IStartupEntry? _entry;

    private volatile StartupAvailability _availability = availability;

    /// <summary>
    /// Whether this copy can start with Windows, and if not, why.
    /// </summary>
    public StartupAvailability Availability => _availability;

    /// <summary>
    /// Whether Riftcaster starts when someone signs in to Windows. Read from Windows each time, so it follows
    /// the entry being removed there.
    /// </summary>
    public bool StartsWithWindows => _entry?.Exists ?? false;

    /// <summary>
    /// Whether starting Riftcaster opens its web interface, unless a launch option says otherwise.
    /// </summary>
    public bool OpenWebUI => settings.Settings.Startup.OpenWebUI;

    /// <summary>
    /// Raised when starting with Windows changes, or the copy turns out to be installed.
    /// </summary>
    /// <remarks>
    /// May be raised on any thread.
    /// </remarks>
    public event Action? Changed;

    /// <summary>
    /// Gives the service the entry to change, which makes this copy able to start with Windows.
    /// </summary>
    /// <param name="entry">The entry Windows starts Riftcaster from.</param>
    public void Attach(IStartupEntry entry)
    {
        lock (_lock)
        {
            _entry = entry;
            _availability = StartupAvailability.Available;
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Says why this copy can't start with Windows, when the Windows app has found out it can't.
    /// </summary>
    /// <param name="reason">Why.</param>
    public void Unavailable(StartupAvailability reason)
    {
        _availability = reason;
        Changed?.Invoke();
    }

    /// <summary>
    /// Adds or removes the entry that starts Riftcaster when someone signs in. Does nothing if this copy can't.
    /// </summary>
    /// <param name="on">Whether it starts with Windows.</param>
    /// <returns>Whether it is as asked now. False if Windows refused, which is logged.</returns>
    public bool SetStartsWithWindows(bool on)
    {
        IStartupEntry? entry;
        lock (_lock)
        {
            entry = _entry;
        }

        if (entry is null)
        {
            return false;
        }

        try
        {
            entry.Set(on);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or SecurityException)
        {
            logger.LogWarning(exception, "Couldn't {Change} the entry that starts Riftcaster with Windows.", on ? "add" : "remove");
        }

        Changed?.Invoke();
        return entry.Exists == on;
    }
}
