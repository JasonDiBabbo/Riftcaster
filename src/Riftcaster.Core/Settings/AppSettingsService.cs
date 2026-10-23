using Riftcaster.Contracts;
using Riftcaster.Core.Match;
using Riftcaster.Core.Network;

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
    /// The rules Reset to defaults gives the current match: the ones chosen on the Settings page, or the
    /// design's defaults (<see cref="MatchRules.Default"/>) until some are.
    /// </summary>
    public MatchSettings MatchDefaults => _settings.MatchDefaults ?? MatchRules.Default;

    /// <summary>
    /// The port the server will listen on the next time it starts, unless started with <c>--urls</c>:
    /// the one chosen on the Settings page, or the default until one is.
    /// </summary>
    public int ChosenPort => _settings.Server.Port ?? ListenPort.Default;

    /// <summary>
    /// Raised after the settings change.
    /// Handlers read <see cref="Settings"/> for the new values.
    /// </summary>
    /// <remarks>
    /// May be raised on any thread.
    /// </remarks>
    public event Action? Changed;

    /// <summary>
    /// Chooses the port the server listens on from the next start. Choosing the default is choosing none,
    /// so a copy that never picked one follows the default.
    /// </summary>
    /// <param name="port">The port.</param>
    public void ChoosePort(int port) =>
        Update(settings => settings with { Server = new ServerSettings { Port = port == ListenPort.Default ? null : port } });

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
        MatchDefaults = settings.MatchDefaults is { } rules ? NormalizeMatch(rules) : null,

        // A port that can't be chosen (a hand-edited file) is none: the default applies, as at the start,
        // when the server ignored it too.
        Server = settings.Server.Port is { } port && !ListenPort.IsValid(port) ? settings.Server with { Port = null } : settings.Server,
    };

    // The same limits as the current match's, and the mode and format put back to the design's if they
    // aren't one of the known ones (the JSON accepts numbers): a mode nobody knows would break the match
    // the moment it was applied.
    private static MatchSettings NormalizeMatch(MatchSettings rules) => MatchService.Normalize(rules with
    {
        Format = Enum.IsDefined(rules.Format) ? rules.Format : MatchRules.Default.Format,
        Mode = Enum.IsDefined(rules.Mode) ? rules.Mode : MatchRules.Default.Mode,
    });
}
