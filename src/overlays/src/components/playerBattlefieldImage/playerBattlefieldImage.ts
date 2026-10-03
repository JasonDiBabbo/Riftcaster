/**
 * Battlefield image overlay for one player: the battlefield card's art, streamed from the server over a
 * WebSocket (/api/match/events). Blank until chosen.
 *
 * Shows the player in ?player=n (default 1), and nothing when the match mode doesn't use that seat.
 * Ported from the first version of this application (app/components/playerBattlefieldImage).
 */
import type { MatchState } from '../../generated';
import { showCardImage } from '../../shared/cardImage';
import { getElement } from '../../shared/dom';
import { playerAt, seatFromUrl } from '../../shared/match';
import { subscribe } from '../../shared/stream';

const seat = seatFromUrl();
const image = getElement<HTMLImageElement>('image');

function render(state: MatchState): void {
  showCardImage(image, playerAt(state, seat)?.battlefield);
}

subscribe<MatchState>('/api/match/events', `Battlefield image (player ${seat + 1})`, render);
