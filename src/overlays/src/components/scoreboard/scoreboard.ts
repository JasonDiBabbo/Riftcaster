/**
 * Scoreboard overlay. Shows the two sides' points as a row of numbered circles, streamed from the
 * server over Server-Sent Events (GET /api/match/events).
 *
 * Each side has a circle for each point from 1 to one short of the points to win, counting towards
 * the middle, where one shared circle stands for the winning point. A side's current score is
 * highlighted in gold. Points still ahead get an "inactive" class, which the first version's CSS
 * styles the same as points already passed, so it's there to restyle if they should differ.
 *
 * The two sides are the two players in 1v1 and the two teams in 2v2. Free-for-all modes have more
 * than two sides, which this layout can't show, so the scoreboard is hidden in them.
 */
import type { MatchState } from '../../generated';
import { getElement } from '../../shared/dom';
import { isTeamMode, playerCount, scoreAt } from '../../shared/match';
import { subscribe } from '../../shared/stream';

// Module scripts run after the document is parsed, so the elements exist here.
const scoreboard = getElement('scoreboard');
const circles = getElement('circles');

// A side of more than this many circles is split over two rows, rather than shrinking the circles.
const maxCirclesPerRow = 7;

/** The circles currently built, and the points to win they were built for. */
interface Built {
  pointsToWin: number;
  /** Each side's circles, indexed by point (index 0 unused). */
  sides: [HTMLElement[], HTMLElement[]];
  center: HTMLElement;
}

let built: Built | null = null;

function makeCircle(point: number): HTMLDivElement {
  const circle = document.createElement('div');
  circle.className = 'score-circle';
  circle.textContent = String(point);
  return circle;
}

function makeRow(points: number[], byPoint: HTMLElement[]): HTMLDivElement {
  const row = document.createElement('div');
  row.className = 'circles-row';
  for (const point of points) {
    row.append((byPoint[point] = makeCircle(point)));
  }
  return row;
}

/**
 * One side's circles, numbered 1 to perSide. Up to 7 sit in one row; more are split over two rows,
 * the first counting 1 to half (rounded up) and the second carrying on. The right side is mirrored
 * so that both sides count towards the middle.
 */
function buildSide(perSide: number, mirrored: boolean, byPoint: HTMLElement[]): HTMLDivElement {
  const side = document.createElement('div');
  side.className = mirrored ? 'circles-right' : 'circles-left';

  const range = (from: number, to: number) => {
    const points = Array.from({ length: Math.max(0, to - from + 1) }, (_, i) => from + i);
    return mirrored ? points.reverse() : points;
  };

  if (perSide <= maxCirclesPerRow) {
    for (const point of range(1, perSide)) {
      side.append((byPoint[point] = makeCircle(point)));
    }
    return side;
  }

  side.classList.add('circles-two-row');
  const topCount = Math.ceil(perSide / 2);
  side.append(makeRow(range(1, topCount), byPoint), makeRow(range(topCount + 1, perSide), byPoint));
  return side;
}

function build(pointsToWin: number): Built {
  const left: HTMLElement[] = [];
  const right: HTMLElement[] = [];

  const centerWrapper = document.createElement('div');
  centerWrapper.className = 'circles-center';
  const center = makeCircle(pointsToWin);
  center.classList.add('center');
  centerWrapper.append(center);

  circles.replaceChildren(
    buildSide(pointsToWin - 1, false, left),
    centerWrapper,
    buildSide(pointsToWin - 1, true, right)
  );

  return { pointsToWin, sides: [left, right], center };
}

/** The two sides' points: the players' in 1v1, the teams' in 2v2, or null in free-for-all. */
function sidePoints(state: MatchState): [number, number] | null {
  if (playerCount(state.settings) !== 2 && !isTeamMode(state.settings)) {
    return null;
  }

  // In 2v2, seat 0 is on Team A and seat 2 on Team B, and scoreAt gives their team's score.
  const [first, second] = isTeamMode(state.settings) ? [0, 2] : [0, 1];
  return [scoreAt(state, first)?.points ?? 0, scoreAt(state, second)?.points ?? 0];
}

function render(state: MatchState): void {
  const points = sidePoints(state);
  scoreboard.hidden = points === null;
  if (points === null) {
    return;
  }

  // Rebuild only when the points to win change; otherwise just restyle the circles.
  const pointsToWin = state.settings.pointsToWin;
  const current = built?.pointsToWin === pointsToWin ? built : (built = build(pointsToWin));

  points.forEach((score, index) => {
    current.sides[index].forEach((circle, point) => {
      circle.classList.toggle('active', point === score);
      circle.classList.toggle('inactive', point > score);
    });
  });

  // The winning point lights up when either side reaches it.
  current.center.classList.toggle('active', points.includes(pointsToWin));
}

subscribe<MatchState>('/api/match/events', 'Scoreboard', render);
