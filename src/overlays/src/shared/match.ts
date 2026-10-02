/**
 * The match rules and seat lookups shared by the match overlays (scoreboard and the per-player
 * overlays), which all follow /api/match/events.
 *
 * The rules are a TypeScript copy of the server's MatchRules (src/Riftcaster.Core/Match): keep the
 * two in step, and their tests too (match.test.ts mirrors MatchRulesTests).
 */
import type { MatchMode, MatchSettings, MatchState, Player } from '../generated';

const playerCounts: Record<MatchMode, number> = {
  OneVsOne: 2,
  TwoVsTwo: 4,
  FreeForAll3: 3,
  FreeForAll4: 4,
};

/** How many players take part: the first 2, 3 or 4 seats are in use. */
export function playerCount(settings: MatchSettings): number {
  return playerCounts[settings.mode];
}

/** Whether players play as teams (2v2), where the teams hold the points and game wins. */
export function isTeamMode(settings: MatchSettings): boolean {
  return settings.mode === 'TwoVsTwo';
}

/** The most games a player or team can win: 1 in a best of 1, 2 in a best of 3. */
export function maxGameWins(settings: MatchSettings): number {
  return settings.format === 'BestOf3' ? 2 : 1;
}

/**
 * The seat an overlay shows, from 0, read from its URL's ?player=n (from 1, as the operator counts
 * them), so the same page works for every player: /overlays/playerName/playerName.html?player=3.
 * Without a valid number, it's player 1.
 */
export function seatFromUrl(search: string = window.location.search): number {
  const player = Number.parseInt(new URLSearchParams(search).get('player') ?? '', 10);
  return player >= 1 ? player - 1 : 0;
}

/** The player in a seat, or null if the match mode doesn't use that seat (e.g. seat 3 in 1v1). */
export function playerAt(state: MatchState, seat: number): Player | null {
  return seat < playerCount(state.settings) ? (state.players.players[seat] ?? null) : null;
}

/** A player's or team's score. */
export interface Score {
  points: number;
  gameWins: number;
}

/**
 * The score that counts for a seat: its team's in 2v2 (the first two seats are Team A, the last
 * two Team B), otherwise the player's own. Null if the match mode doesn't use that seat.
 */
export function scoreAt(state: MatchState, seat: number): Score | null {
  const player = playerAt(state, seat);
  if (player === null) {
    return null;
  }

  return isTeamMode(state.settings) ? (state.players.teams[Math.floor(seat / 2)] ?? null) : player;
}
