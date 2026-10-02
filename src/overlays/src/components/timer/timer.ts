/**
 * Timer overlay. Shows the match timer, streamed from the server over Server-Sent Events
 * (GET /api/timer/events): the current state on connect, then one event per change (start, pause,
 * adjust and so on), never one per second.
 *
 * Between events this page counts by itself, from when the last event arrived (see elapsedAt in
 * shared/timer.ts). In overtime it counts up as +mm:ss; without overtime it stops at 00:00.
 */
import type { TimerState } from '../../generated';
import { getElement } from '../../shared/dom';
import { subscribe } from '../../shared/stream';
import { elapsedAt, readTimer, type TimerSnapshot } from '../../shared/timer';

// Module scripts run after the document is parsed, so the element exists here.
const display = getElement('timerDisplay');

let snapshot: TimerSnapshot | null = null;

function draw(): void {
  if (snapshot === null) {
    return; // nothing received yet
  }

  const { time, overtime } = readTimer(snapshot.state, elapsedAt(snapshot, performance.now()));
  if (display.textContent !== time) {
    display.textContent = time;
  }
  display.classList.toggle('is-overtime', overtime);
}

subscribe<TimerState>('/api/timer/events', 'Timer', (state) => {
  snapshot = { state, receivedAt: performance.now() };
  draw();
});

// Ten times a second, so each new second shows within 100ms of when it starts. Drawing an
// unchanged time costs nothing: the text is only replaced when it changes.
setInterval(draw, 100);
