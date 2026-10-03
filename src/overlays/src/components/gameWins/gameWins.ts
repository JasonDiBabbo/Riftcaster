/**
 * Game wins overlay for one player: a circle for each game that can be won (1 in a best of 1,
 * 2 in a best of 3), with the Riftbound logo lit in each one won. In 2v2 it shows the player's
 * team's wins. Streamed from the server over a WebSocket (/api/match/events).
 *
 * Shows the player in ?player=n (default 1), and nothing when the match mode doesn't use that seat.
 */
import type { MatchState } from '../../generated';
import { getElement } from '../../shared/dom';
import { maxGameWins, scoreAt, seatFromUrl } from '../../shared/match';
import { subscribe } from '../../shared/stream';

const seat = seatFromUrl();
const container = getElement('gameWins');

function makeCircle(won: boolean): HTMLDivElement {
  const circle = document.createElement('div');
  circle.className = won ? 'game-win-circle filled' : 'game-win-circle';

  const logo = document.createElement('img');
  logo.className = 'game-win-logo';
  logo.src = 'riftbound-logo.svg';
  logo.alt = '';
  circle.append(logo);

  return circle;
}

function render(state: MatchState): void {
  const score = scoreAt(state, seat);
  if (score === null) {
    container.replaceChildren();
    return;
  }

  const circles = Array.from({ length: maxGameWins(state.settings) }, (_, i) =>
    makeCircle(i < score.gameWins)
  );
  container.replaceChildren(...circles);
}

subscribe<MatchState>('/api/match/events', `Game wins (player ${seat + 1})`, render);
