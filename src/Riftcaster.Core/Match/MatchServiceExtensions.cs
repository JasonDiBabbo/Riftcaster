using Riftcaster.Core.Settings;

namespace Riftcaster.Core.Match;

/// <summary>
/// What the match and the Settings page's match defaults do together.
/// </summary>
public static class MatchServiceExtensions
{
    extension(MatchService match)
    {
        /// <summary>
        /// Gives the current match the rules chosen as the match defaults: the mode, format, points to win,
        /// duration and overtime. Scores, names and cards are kept.
        /// </summary>
        /// <param name="settings">The settings that hold the match defaults.</param>
        public void ResetToDefaults(AppSettingsService settings) => match.Update(_ => settings.MatchDefaults);
    }
}
