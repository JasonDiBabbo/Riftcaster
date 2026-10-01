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
        MatchMode.TwoVsTwo => TeamName(TeamOf(seat)),
        _ => $"Seat {seat + 1}",
    };

    /// <summary>
    /// A player's name, or "Player n" until the operator enters one.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="seat">The player's seat, from 0.</param>
    public static string Name(Player player, int seat) =>
        string.IsNullOrEmpty(player.Name) ? $"Player {seat + 1}" : player.Name;

    /// <summary>
    /// "Team A" or "Team B".
    /// </summary>
    /// <param name="team">0 for Team A, 1 for Team B.</param>
    public static string TeamName(int team) => team == 0 ? "Team A" : "Team B";

    /// <summary>
    /// The team a seat belongs to in 2v2: the first two seats are Team A (0), the last two Team B (1).
    /// </summary>
    /// <param name="seat">The player's seat, from 0.</param>
    public static int TeamOf(int seat) => seat / 2;

    /// <summary>
    /// The seats of a team's two players in 2v2.
    /// </summary>
    /// <param name="team">0 for Team A, 1 for Team B.</param>
    public static IEnumerable<int> SeatsOf(int team) => Enumerable.Range(team * 2, 2);

    /// <summary>
    /// The hue of a player's colour: one per seat, or one per team in 2v2 (Team A blue, Team B red).
    /// </summary>
    /// <param name="settings">The match settings.</param>
    /// <param name="seat">The player's seat, from 0.</param>
    public static int Hue(MatchSettings settings, int seat) =>
        settings.IsTeamMode ? TeamHue(TeamOf(seat)) : SeatHues[seat];

    /// <summary>
    /// The hue of a team's colour: Team A blue, Team B red, the same as their first two seats.
    /// </summary>
    /// <param name="team">0 for Team A, 1 for Team B.</param>
    public static int TeamHue(int team) => SeatHues[team];
}
