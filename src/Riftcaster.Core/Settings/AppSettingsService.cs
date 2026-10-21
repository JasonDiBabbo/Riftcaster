using Riftcaster.Contracts;

namespace Riftcaster.Core.Settings;

/// <summary>
/// The service for reading and changing the Settings page's settings.
/// </summary>
/// <remarks>
/// The settings are saved as they change. What a setting does (its effect on logging, say) is up to
/// whatever listens to <see cref="Changed"/>: this service only keeps the values.
/// </remarks>
/// <param name="store">Keeps the settings between runs.</param>
public sealed class AppSettingsService(IAppSettingsStore store)
{
    private readonly IAppSettingsStore _store = store;

    private readonly Lock _lock = new();

    private AppSettings _settings = Normalize(store.Load() ?? new AppSettings());

    /// <summary>
    /// The current settings.
    /// </summary>
    public AppSettings Settings => _settings;

    /// <summary>
    /// Raised after the settings change.
    /// Handlers read <see cref="Settings"/> for the new values.
    /// </summary>
    /// <remarks>
    /// May be raised on any thread.
    /// </remarks>
    public event Action? Changed;

    /// <summary>
    /// Changes the settings, for example <c>Update(settings => settings with { Logging = new() { Detail = LogDetail.Detailed } })</c>.
    /// A value outside the allowed ones is put back to its default.
    /// </summary>
    /// <param name="change">
    /// Builds the new settings from the current ones. Runs inside the lock, so keep it to a
    /// <c>with</c> expression: don't call back into this service from it.
    /// </param>
    public void Update(Func<AppSettings, AppSettings> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        lock (_lock)
        {
            var updated = Normalize(change(_settings));
            if (updated == _settings)
            {
                return;
            }

            _settings = updated;
            _store.Save(updated); // Inside the lock, so saves happen in the same order as the changes
        }

        Changed?.Invoke();
    }

    private static AppSettings Normalize(AppSettings settings) => settings with
    {
        Logging = Enum.IsDefined(settings.Logging.Detail) ? settings.Logging : settings.Logging with { Detail = LogDetail.Normal },
    };
}
