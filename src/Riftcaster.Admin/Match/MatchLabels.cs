using Riftcaster.Admin.Shared;
using Riftcaster.Contracts;

namespace Riftcaster.Admin.Match;

/// <summary>
/// How the match settings are labelled in the admin: the panel's options, and the header's summary.
/// </summary>
public static class MatchLabels
{
    /// <summary>
    /// The labels of match formats.
    /// </summary>
    public static IReadOnlyList<SegmentOption<MatchFormat>> FormatOptions { get; } =
    [
        new(MatchFormat.BestOf1, "Best of 1"),
        new(MatchFormat.BestOf3, "Best of 3"),
    ];

    /// <summary>
    /// The labels of modes of play.
    /// </summary>
    public static IReadOnlyList<SegmentOption<MatchMode>> ModeOptions { get; } =
    [
        new(MatchMode.OneVsOne, "1v1"),
        new(MatchMode.TwoVsTwo, "2v2"),
        new(MatchMode.FreeForAll3, "FFA 3"),
        new(MatchMode.FreeForAll4, "FFA 4"),
    ];

    /// <summary>
    /// The header's one-line summary, e.g. "Bo3 · 1v1 · 8 pts · 50 min + OT".
    /// </summary>
    public static string Summary(MatchSettings settings)
    {
        var format = settings.Format == MatchFormat.BestOf3 ? "Bo3" : "Bo1";
        var mode = ModeOptions.First(option => option.Value == settings.Mode).Label;
        var overtime = settings.AllowOvertime ? " + OT" : "";
        return $"{format} · {mode} · {settings.PointsToWin} pts · {settings.DurationMinutes} min{overtime}";
    }
}
