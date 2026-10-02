import { describe, expect, it } from 'vitest';
import { reconnectDelay, socketUrl } from './stream';

describe('socketUrl', () => {
  it('uses ws:// for a page served over http://', () => {
    expect(socketUrl('/api/timer/events', 'http://localhost:5062/overlays/timer/timer.html')).toBe(
      'ws://localhost:5062/api/timer/events'
    );
  });

  it('uses wss:// for a page served over https://', () => {
    expect(
      socketUrl('/api/timer/events', 'https://192.168.1.20:7216/overlays/timer/timer.html')
    ).toBe('wss://192.168.1.20:7216/api/timer/events');
  });
});

describe('reconnectDelay', () => {
  it.each([
    [0, 1000],
    [1, 2000],
    [2, 4000],
    [3, 8000],
    [4, 10_000], // Capped
    [20, 10_000],
  ])('waits after attempt %i: %i ms', (attempt, expected) => {
    expect(reconnectDelay(attempt)).toBe(expected);
  });
});
