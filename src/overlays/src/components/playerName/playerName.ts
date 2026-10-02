/**
 * Player name overlay for one player, streamed from the server over Server-Sent Events
 * (GET /api/match/events).
 *
 * Shows the player in ?player=n (default 1), and nothing when the match mode doesn't use that seat.
 */
import type { MatchState } from '../../generated';
import { getElement } from '../../shared/dom';
import { playerAt, seatFromUrl } from '../../shared/match';
import { subscribe } from '../../shared/stream';

const seat = seatFromUrl();
const container = getElement('container');
const name = getElement('playerName');

function render(state: MatchState): void {
  const player = playerAt(state, seat);
  container.hidden = player === null;
  name.textContent = player?.name ?? '';
}

subscribe<MatchState>('/api/match/events', `Player name (player ${seat + 1})`, render);
