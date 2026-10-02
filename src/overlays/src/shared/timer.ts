/**
 * The match timer's display logic, shared by the overlays that show it. A TypeScript copy of the
 * server's TimerFormat and TimerReadout (src/Riftcaster.Core/Timer): keep the two in step, and
 * their tests too (timer.test.ts mirrors TimerFormatTests and TimerReadoutTests).
 */
import type { TimerState } from '../generated';

/** A timer state, and when it arrived (performance.now(), in milliseconds). */
export interface TimerSnapshot {
  state: TimerState;
  receivedAt: number;
}

/** What a timer overlay shows. */
export interface TimerReading {
  /** The time remaining as mm:ss, or the time past zero as +mm:ss. */
  time: string;
  /** Whether the timer is past zero, with overtime allowed. */
  overtime: boolean;
}

/**
 * The time run as of `now` (performance.now()). A running timer counts forward from when its
 * snapshot arrived, using this page's own clock, so it doesn't matter whether the computer's
 * clock agrees with the server's.
 */
export function elapsedAt(snapshot: TimerSnapshot, now: number): number {
  const { state, receivedAt } = snapshot;
  return state.running
    ? state.elapsedMilliseconds + Math.max(0, now - receivedAt)
    : state.elapsedMilliseconds;
}

/**
 * Formats whole seconds as mm:ss, e.g. 3000 as "50:00" and 65 as "01:05". Minutes aren't capped
 * at 59: 3 hours is "180:00".
 */
export function formatTime(seconds: number): string {
  if (!Number.isInteger(seconds) || seconds < 0) {
    throw new RangeError(`Expected whole seconds, 0 or more; got ${seconds}.`);
  }

  const minutes = Math.floor(seconds / 60);
  return `${String(minutes).padStart(2, '0')}:${String(seconds % 60).padStart(2, '0')}`;
}

/**
 * What to show for a timer that has run `elapsedMilliseconds`.
 *
 * Counting down, it rounds up: a timer started at 50:00 shows 50:00 for its first second and only
 * reaches 00:00 at zero. Past zero it rounds down, like a stopwatch. Without overtime it stays at
 * 00:00, since the server's stop can arrive a moment after this page reaches zero.
 */
export function readTimer(state: TimerState, elapsedMilliseconds: number): TimerReading {
  const remaining = state.totalSeconds * 1000 - elapsedMilliseconds;
  const overtime = remaining < 0 && state.allowOvertime;

  const time = overtime
    ? `+${formatTime(Math.floor(-remaining / 1000))}`
    : formatTime(Math.ceil(Math.max(0, remaining) / 1000));

  return { time, overtime };
}
