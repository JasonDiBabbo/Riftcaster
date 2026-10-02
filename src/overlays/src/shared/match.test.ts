// The rules mirror MatchRulesTests (tests/Riftcaster.Core.Tests): the overlays and the server must
// agree on which seats are in use and how many games can be won.
import { describe, expect, it } from 'vitest';
import type { MatchFormat, MatchMode, MatchState, Player } from '../generated';
import { isTeamMode, maxGameWins, playerAt, playerCount, scoreAt, seatFromUrl } from './match';

function player(name: string, points = 0, gameWins = 0): Player {
  return { name, legend: null, champion: null, battlefield: null, points, gameWins, xp: 0 };
}

function state(mode: MatchMode = 'OneVsOne', format: MatchFormat = 'BestOf3'): MatchState {
  return {
    settings: { pointsToWin: 8, format, mode, durationMinutes: 50, allowOvertime: true },
    players: {
      players: [player('Mara', 5, 1), player('Dex', 3), player('Juno', 2), player('Theo', 7, 2)],
      teams: [
        { points: 4, gameWins: 1 },
        { points: 6, gameWins: 0 },
      ],
    },
  };
}

describe('match rules', () => {
  it.each([
    ['OneVsOne', 2],
    ['TwoVsTwo', 4],
    ['FreeForAll3', 3],
    ['FreeForAll4', 4],
  ] as const)('%s has %i players', (mode, expected) => {
    expect(playerCount(state(mode).settings)).toBe(expected);
  });

  it.each([
    ['BestOf1', 1],
    ['BestOf3', 2],
  ] as const)('%s allows %i game wins', (format, expected) => {
    expect(maxGameWins(state('OneVsOne', format).settings)).toBe(expected);
  });

  it('is a team mode only for 2v2', () => {
    expect(isTeamMode(state('TwoVsTwo').settings)).toBe(true);
    expect(isTeamMode(state('FreeForAll4').settings)).toBe(false);
  });
});

describe('seatFromUrl', () => {
  it.each([
    ['?player=1', 0],
    ['?player=3', 2],
    ['', 0], // No player: player 1
    ['?player=0', 0],
    ['?player=abc', 0],
  ])('reads %s as seat %i', (search, expected) => {
    expect(seatFromUrl(search)).toBe(expected);
  });
});

describe('playerAt', () => {
  it('finds a seat in use', () => {
    expect(playerAt(state('FreeForAll3'), 2)?.name).toBe('Juno');
  });

  it('is null for a seat the mode does not use', () => {
    expect(playerAt(state('OneVsOne'), 2)).toBeNull();
  });
});

describe('scoreAt', () => {
  it("is the player's own score outside 2v2", () => {
    expect(scoreAt(state('OneVsOne'), 0)).toMatchObject({ points: 5, gameWins: 1 });
  });

  it("is the team's score in 2v2", () => {
    const match = state('TwoVsTwo');

    expect(scoreAt(match, 1)).toEqual({ points: 4, gameWins: 1 }); // Team A
    expect(scoreAt(match, 3)).toEqual({ points: 6, gameWins: 0 }); // Team B
  });

  it('is null for a seat the mode does not use', () => {
    expect(scoreAt(state('OneVsOne'), 3)).toBeNull();
  });
});
