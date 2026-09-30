using Riftcaster.Contracts;

namespace Riftcaster.Core.Match;

/// <summary>
/// The service for interacting with and modifying match settings.
/// </summary>
public class MatchService(IMatchStore store)
{
    /// <summary>
    /// The lowest allowed points to win.
    /// </summary>
    public const int MinPointsToWin = 1;

    /// <summary>
    /// The shortest allowed match in minutes.
    /// </summary>
    public const int MinDurationMinutes = 1;

    private readonly IMatchStore _store = store;

    private readonly Lock _lock = new();

    private MatchSettings _settings = Normalize(store.Load() ?? MatchRules.Default);

    /// <summary>
    /// The current match settings.
    /// </summary>
    public MatchSettings Settings => _settings;

    /// <summary>
    /// Raised after the match settings change.
    /// Handlers read <see cref="Settings"/> for the new values.
    /// </summary>
    /// <remarks>
    /// May be raised on any thread.
    /// </remarks>
    public event Action? Changed;

    /// <summary>
    /// Changes the settings, for example <c>Update(settings => settings with { Mode = MatchMode.TwoVsTwo })</c>.
    /// Values below the minimums are raised to them.
    /// </summary>
    /// <param name="change">
    /// Builds the new settings from the current ones. Runs inside the lock, so keep it to a
    /// <c>with</c> expression: don't call back into this service from it.
    /// </param>
    public void Update(Func<MatchSettings, MatchSettings> change)
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

    private static MatchSettings Normalize(MatchSettings settings) => settings with
    {
        PointsToWin = Math.Max(MinPointsToWin, settings.PointsToWin),
        DurationMinutes = Math.Max(MinDurationMinutes, settings.DurationMinutes),
    };
}
