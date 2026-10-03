/**
 * Legend image overlay for one player: the legend card's art, streamed from the server over a
 * WebSocket (/api/match/events). Blank until chosen.
 *
 * Shows the player in ?player=n (default 1), and nothing when the match mode doesn't use that seat.
 * Ported from the first version of this application (app/components/playerLegendImage).
 */
import type { MatchState } from '../../generated';
import { showCardImage } from '../../shared/cardImage';
import { getElement } from '../../shared/dom';
import { playerAt, seatFromUrl } from '../../shared/match';
import { subscribe } from '../../shared/stream';

const seat = seatFromUrl();
const image = getElement<HTMLImageElement>('image');

function render(state: MatchState): void {
  showCardImage(image, playerAt(state, seat)?.legend);
}

subscribe<MatchState>('/api/match/events', `Legend image (player ${seat + 1})`, render);
