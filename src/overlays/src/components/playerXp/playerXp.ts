/**
 * XP overlay for one player, streamed from the server over a WebSocket
 * (/api/match/events). XP is per player in every mode, including 2v2.
 *
 * Shows the player in ?player=n (default 1), and nothing when the match mode doesn't use that seat.
 */
import type { MatchState } from '../../generated';
import { getElement } from '../../shared/dom';
import { playerAt, seatFromUrl } from '../../shared/match';
import { subscribe } from '../../shared/stream';

const seat = seatFromUrl();
const container = getElement('container');
const label = getElement('xpLabel');
const value = getElement('xpValue');

function render(state: MatchState): void {
  const player = playerAt(state, seat);
  container.hidden = player === null;
  const xp = player?.xp ?? 0;

  // Below 1 XP there's nothing worth reporting: the "XP" label disappears and the value box is
  // blank rather than "0", so the overlay stays unobtrusive until a player has XP to show off.
  label.classList.toggle('hidden', xp <= 0);
  value.textContent = xp > 0 ? String(xp) : '';
}

subscribe<MatchState>('/api/match/events', `Player XP (player ${seat + 1})`, render);
