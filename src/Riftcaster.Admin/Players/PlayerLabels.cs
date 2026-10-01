using Riftcaster.Admin.Match;
using Riftcaster.Contracts;
using Riftcaster.Core.Match;

namespace Riftcaster.Admin.Players;

/// <summary>
/// How players are labelled and coloured in the admin, which depends on the match mode.
/// </summary>
public static class PlayerLabels
{
    // Blue, red/coral, yellow and purple: oklch(0.74 0.13 H) with these hues.
    private static readonly int[] SeatHues = [250, 25, 85, 310];

    /// <summary>
    /// The Players panel's meta, e.g. "1v1 · 2 players".
    /// </summary>
    public static string Meta(MatchSettings settings)
    {
        var mode = MatchLabels.ModeOptions.First(option => option.Value == settings.Mode).Label;
        return $"{mode} · {settings.PlayerCount} players";
    }

    /// <summary>
    /// The label beside "PLAYER n": Left or Right in 1v1, the team in 2v2, otherwise the seat.
    /// </summary>
    /// <param name="settings">The match settings.</param>
    /// <param name="seat">The player's seat, from 0.</param>
    public static string Side(MatchSettings settings, int seat) => settings.Mode switch
    {
        MatchMode.OneVsOne => seat == 0 ? "Left" : "Right",
        MatchMode.TwoVsTwo => seat < 2 ? "Team A" : "Team B",
        _ => $"Seat {seat + 1}",
    };

    /// <summary>
    /// The hue of a player's colour: one per seat, or one per team in 2v2 (Team A blue, Team B red).
    /// </summary>
    /// <param name="settings">The match settings.</param>
    /// <param name="seat">The player's seat, from 0.</param>
    public static int Hue(MatchSettings settings, int seat) =>
        settings.IsTeamMode ? SeatHues[seat < 2 ? 0 : 1] : SeatHues[seat];
}
