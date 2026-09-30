using Riftcaster.Contracts;

namespace Riftcaster.Core.Match;

/// <summary>
/// Loads and saves the match settings, so they survive a server restart.
/// </summary>
public interface IMatchStore
{
    /// <summary>
    /// Loads the saved settings.
    /// </summary>
    /// <returns>
    /// The saved settings, or <see langword="null"/> if there are none (the service then uses the defaults).
    /// </returns>
    MatchSettings? Load();

    /// <summary>
    /// Saves the settings, replacing any previous save.
    /// </summary>
    /// <remarks>
    /// Implementations should log failures rather than throw exceptions.
    /// </remarks>
    void Save(MatchSettings settings);
}
