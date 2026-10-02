// Mirrors TimerFormatTests and TimerReadoutTests (tests/Riftcaster.Core.Tests): the overlay and the
// admin must show the same time for the same state.
import { describe, expect, it } from 'vitest';
import type { TimerState } from '../generated';
import { elapsedAt, formatTime, readTimer } from './timer';

const total = 50 * 60;

function state(elapsedMilliseconds: number, running = true, allowOvertime = true): TimerState {
  return { totalSeconds: total, elapsedMilliseconds, running, allowOvertime };
}

describe('formatTime', () => {
  it.each([
    [0, '00:00'],
    [5, '00:05'],
    [65, '01:05'],
    [3000, '50:00'],
    [10800, '180:00'],
  ])('formats %i seconds as %s', (seconds, expected) => {
    expect(formatTime(seconds)).toBe(expected);
  });

  it.each([-1, 1.5])('rejects %d', (seconds) => {
    expect(() => formatTime(seconds)).toThrow(RangeError);
  });
});

describe('readTimer', () => {
  it.each([
    [0, '50:00'],
    [1, '50:00'], // Rounds up while counting down
    [1_000, '49:59'],
    [total * 1000 - 1, '00:01'],
    [total * 1000, '00:00'],
    [total * 1000 + 999, '+00:00'], // Rounds down past zero
    [total * 1000 + 65_000, '+01:05'],
  ])('shows %i ms run as %s', (elapsed, expected) => {
    expect(readTimer(state(elapsed), elapsed).time).toBe(expected);
  });

  it('is in overtime only past zero', () => {
    expect(readTimer(state(total * 1000), total * 1000).overtime).toBe(false);
    expect(readTimer(state(total * 1000 + 1), total * 1000 + 1).overtime).toBe(true);
  });

  it('stays at zero past it without overtime', () => {
    // The server stops it at zero; this covers the moment before its stop arrives.
    const reading = readTimer(state(total * 1000 + 300, true, false), total * 1000 + 300);

    expect(reading).toEqual({ time: '00:00', overtime: false });
  });
});

describe('elapsedAt', () => {
  it('counts forward from when a running snapshot arrived', () => {
    expect(elapsedAt({ state: state(10_000), receivedAt: 500 }, 2_500)).toBe(12_000);
  });

  it('stays put while paused', () => {
    expect(elapsedAt({ state: state(10_000, false), receivedAt: 500 }, 2_500)).toBe(10_000);
  });

  it('never counts backwards', () => {
    expect(elapsedAt({ state: state(10_000), receivedAt: 500 }, 400)).toBe(10_000);
  });
});
