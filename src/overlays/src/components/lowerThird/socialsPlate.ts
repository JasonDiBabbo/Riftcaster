/**
 * The Socials lower third's plate: a navy body in a thin gold ring, notched at every
 * corner, with gold filigree along the top and bottom edges and a hex gem at the bottom
 * centre. Ported from the Socials design handoff's prototype (riftcaster-lower-third-v3.jsx:
 * SCALLOP, Framed, Gilded and HexGem), which is the source of every number here.
 *
 * Returned as SVG markup; nothing in it comes from user input.
 */

/** Plate height, in px; must match socials.css's .socials. The width depends on the handles (see socialsVisual.ts). */
const PLATE_HEIGHT = 78;

/** Width of the gold ring around the navy body. */
const RING = 1.9;

/** Radius of the concave notch at each corner, centred on the corner itself. */
const NOTCH = 12.1;

const GOLD = '#c9a45e';

/**
 * A rectangle with concave quarter-circle notches at all four corners. Sweep flag 0
 * curves each arc inward, toward the middle of the plate.
 */
function notchedRect(x0: number, y0: number, x1: number, y1: number, r: number): string {
  return [
    `M${x0 + r},${y0}`,
    `H${x1 - r}`,
    `A${r} ${r} 0 0 0 ${x1},${y0 + r}`,
    `V${y1 - r}`,
    `A${r} ${r} 0 0 0 ${x1 - r},${y1}`,
    `H${x0 + r}`,
    `A${r} ${r} 0 0 0 ${x0},${y1 - r}`,
    `V${y0 + r}`,
    `A${r} ${r} 0 0 0 ${x0 + r},${y0}`,
    'Z',
  ].join(' ');
}

/**
 * The filigree lines: along each edge, a thin line runs inside the plate, angles out
 * through the border, runs briefly outside, and angles back to merge into the border.
 * The bottom line splits around the gem; the top one is continuous.
 */
function filigreePaths(w: number, h: number): string[] {
  const cx = w / 2;
  const inside = 9; // distance inside the edge
  const outside = 7; // distance outside the edge
  const diagonal = 6; // horizontal run of each angled segment
  const cross = Math.min(150, Math.max(70, w * 0.29)); // where the line exits through the border
  const merge = 34; // where it rejoins the border
  const gemGap = 16; // half-width of the gap around the gem

  // One half of an edge's line. edgeY: the edge; outward: +1 below it, -1 above it.
  const half = (edgeY: number, outward: number, side: -1 | 1, splitForGem: boolean) => {
    const x = (value: number) => (side < 0 ? value : w - value);
    const insideY = edgeY - outward * inside;
    const outsideY = edgeY + outward * outside;
    const points: [number, number][] = splitForGem
      ? [
          [cx - gemGap, edgeY - outward * 3],
          [cx - gemGap - diagonal, insideY],
        ]
      : [[cx, insideY]];
    points.push(
      [cross + diagonal, insideY],
      [cross - diagonal, outsideY],
      [merge + diagonal, outsideY],
      [merge, edgeY]
    );
    return 'M' + points.map(([px, py]) => `${x(px)} ${py}`).join(' L');
  };

  return [half(h, 1, -1, true), half(h, 1, 1, true), half(0, -1, -1, false), half(0, -1, 1, false)];
}

/** The plate for a given width: ring, body and filigree. The gem is separate (gemSvg). */
export function plateSvg(w: number): string {
  const h = PLATE_HEIGHT;
  const band = RING + 6; // the border plus 3px either side, cut out of the filigree
  const lines = filigreePaths(w, h)
    .map((d) => `<path d="${d}" />`)
    .join('');

  return `
    <svg width="${w}" height="${h}" viewBox="0 0 ${w} ${h}" overflow="visible">
      <defs>
        <linearGradient id="socials-ring" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stop-color="#ecd49a" />
          <stop offset="0.5" stop-color="${GOLD}" />
          <stop offset="1" stop-color="#7a5c26" />
        </linearGradient>
        <linearGradient id="socials-body" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stop-color="#172040" />
          <stop offset="1" stop-color="#0d1327" />
        </linearGradient>
        <!-- Hides the filigree where it crosses the border, so it reads as passing under it -->
        <mask id="socials-filigree-cut" maskUnits="userSpaceOnUse" x="-20" y="-40" width="${w + 40}" height="${h + 80}">
          <rect x="-20" y="-40" width="${w + 40}" height="${h + 80}" fill="#fff" />
          <rect x="-20" y="${h - RING - 3}" width="${w + 40}" height="${band}" fill="#000" />
          <rect x="-20" y="-3" width="${w + 40}" height="${band}" fill="#000" />
        </mask>
      </defs>
      <path d="${notchedRect(0, 0, w, h, NOTCH)}" fill="url(#socials-ring)" />
      <path d="${notchedRect(RING, RING, w - RING, h - RING, NOTCH)}" fill="url(#socials-body)" />
      <g mask="url(#socials-filigree-cut)" fill="none" stroke="${GOLD}" stroke-width="1.25" stroke-linejoin="miter">
        ${lines}
      </g>
    </svg>`;
}

/**
 * The 20x20 hex gem: six triangular facets meeting at the centre, each shaded from a
 * pale highlight at the centre toward its own colour, with faint spokes, a centre glow
 * and a dark outline.
 */
export function gemSvg(): string {
  const size = 20;
  const r = size / 2;
  const facetColors = ['#f6d93a', '#d9a70f', '#a97a06', '#c08a0a', '#e3b014', '#fbe463'];
  const vertices = [0, 1, 2, 3, 4, 5].map((i) => {
    const angle = (Math.PI / 180) * (60 * i - 90); // pointy top
    return [r + r * Math.cos(angle), r + r * Math.sin(angle)] as const;
  });
  const next = (i: number) => vertices[(i + 1) % 6];

  const gradients = vertices
    .map((vertex, i) => {
      const [nx, ny] = next(i);
      const midX = (vertex[0] + nx) / 2;
      const midY = (vertex[1] + ny) / 2;
      return `
        <linearGradient id="socials-gem-${i}" gradientUnits="userSpaceOnUse" x1="${r}" y1="${r}" x2="${midX}" y2="${midY}">
          <stop offset="0" stop-color="#fff6b8" />
          <stop offset="0.35" stop-color="${facetColors[i]}" />
          <stop offset="1" stop-color="${facetColors[i]}" />
        </linearGradient>`;
    })
    .join('');
  const facets = vertices
    .map((vertex, i) => {
      const [nx, ny] = next(i);
      return `<polygon points="${r},${r} ${vertex[0]},${vertex[1]} ${nx},${ny}" fill="url(#socials-gem-${i})" />`;
    })
    .join('');
  const spokes = vertices
    .map(
      ([vx, vy]) =>
        `<line x1="${r}" y1="${r}" x2="${vx}" y2="${vy}" stroke="#fff3a6" stroke-opacity="0.45" stroke-width="0.4" />`
    )
    .join('');
  const outline = vertices.map(([vx, vy]) => `${vx},${vy}`).join(' ');

  return `
    <svg width="${size}" height="${size}" viewBox="0 0 ${size} ${size}" overflow="visible">
      <defs>
        ${gradients}
        <radialGradient id="socials-gem-glow" cx="0.5" cy="0.5" r="0.5">
          <stop offset="0" stop-color="#fffbe0" stop-opacity="0.95" />
          <stop offset="0.3" stop-color="#fff2a0" stop-opacity="0.4" />
          <stop offset="1" stop-color="#fff2a0" stop-opacity="0" />
        </radialGradient>
      </defs>
      ${facets}
      ${spokes}
      <circle cx="${r}" cy="${r}" r="${r * 0.7}" fill="url(#socials-gem-glow)" />
      <polygon points="${outline}" fill="none" stroke="#8a6206" stroke-width="0.6" />
    </svg>`;
}
