using Riftcaster.Contracts;

namespace Riftcaster.Core.Match;

/// <summary>
/// The match's default rules, and the values that follow from its configured settings.
/// </summary>
public static class MatchRules
{
    /// <summary>
    /// The design's defaults: 8 points to win, best of 3, 1v1, 50 minutes, overtime allowed.
    /// </summary>
    public static MatchSettings Default { get; } = new(
        PointsToWin: 8,
        Format: MatchFormat.BestOf3,
        Mode: MatchMode.OneVsOne,
        DurationMinutes: 50,
        AllowOvertime: true);

    extension(MatchSettings settings)
    {
        /// <summary>
        /// Gets how many players participate in a match mode.
        /// </summary>
        public int PlayerCount => settings.Mode switch
        {
            MatchMode.OneVsOne => 2,
            MatchMode.TwoVsTwo => 4,
            MatchMode.FreeForAll3 => 3,
            MatchMode.FreeForAll4 => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(settings), settings.Mode, "Unknown match mode."),
        };

        /// <summary>
        /// Gets whether a match mode is composed of teams.
        /// </summary>
        public bool IsTeamMode => settings.Mode == MatchMode.TwoVsTwo;

        /// <summary>
        /// Gets the max number of game wins a player or team can gain in a match format.
        /// </summary>
        public int MaxGameWins => settings.Format switch
        {
            MatchFormat.BestOf1 => 1,
            MatchFormat.BestOf3 => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(settings), settings.Format, "Unknown match format."),
        };
    }
}
