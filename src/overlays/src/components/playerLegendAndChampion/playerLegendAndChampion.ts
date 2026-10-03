/**
 * Legend and champion overlay for one player: the two names, one above the other, streamed from
 * the server over a WebSocket (/api/match/events). Either is blank until chosen.
 *
 * Shows the player in ?player=n (default 1), and nothing when the match mode doesn't use that seat.
 */
import type { MatchState } from '../../generated';
import { getElement } from '../../shared/dom';
import { playerAt, seatFromUrl } from '../../shared/match';
import { subscribe } from '../../shared/stream';

const seat = seatFromUrl();
const container = getElement('container');
const legend = getElement('legendName');
const champion = getElement('championName');

function render(state: MatchState): void {
  const player = playerAt(state, seat);
  container.hidden = player === null;
  legend.textContent = player?.legend?.name ?? '';
  champion.textContent = player?.champion?.name ?? '';
}

subscribe<MatchState>('/api/match/events', `Legend and champion (player ${seat + 1})`, render);
