/**
 * The Socials lower third: a gilded plate that slides up from below the frame and
 * cycles through the entry's links one at a time, in order, until it's hidden.
 * Implements the Socials design handoff (README.md there is the spec; every timing and
 * size below comes from it). Styles: socials.css. Plate artwork: plate.ts.
 *
 * Animated with requestAnimationFrame rather than CSS transitions: the slide is
 * reversible mid-way (a value that moves toward a target), and the cycle's cross-fade
 * is a function of time since the plate was shown.
 */
import { getElement } from '../../shared/dom';
import type { LowerThirdSocialsMessage, SocialLink } from '../../generated';
import { socialIconPaths } from './socialIcons';
import { gemSvg, plateSvg } from './plate';
import type { LowerThirdVisual } from './visual';

const SHOW_SECONDS = 0.7;
const HIDE_SECONDS = 0.6;

/** From resting position to fully below a 1080px frame, plus a 40px margin. */
const SLIDE_PX = 166;

/** Each link's time on screen, including its fade in and out. */
const SLOT_SECONDS = 8;

/** Must match socials.css's .socials. The width depends on the handles (see plateWidth). */
const PLATE_HEIGHT = 78;
const FADE_SECONDS = Math.min(1.5, SLOT_SECONDS / 2 - 0.05);

// Must match socials.css's .socials__handle.
const HANDLE_FONT = "600 27px 'Roboto Condensed Variable'";
const HANDLE_FONT_SIZE = 27;
const HANDLE_LETTER_SPACING_EM = 0.02;

const ICON_SIZE = 24;
const ICON_GAP = 12;
const SIDE_PADDING = 52 * 2 + 8;
const MIN_WIDTH = 240;
const MAX_WIDTH = 520;

const easeInOutCubic = (x: number) => (x < 0.5 ? 4 * x * x * x : 1 - Math.pow(-2 * x + 2, 3) / 2);
const clamp = (value: number, min = 0, max = 1) => Math.min(max, Math.max(min, value));

/**
 * Resolves once the handle font has loaded (or after 3s, so a font that never arrives
 * can't block the overlay). The width is measured with the real font; measuring with a
 * fallback would make the plate change width on its first frame.
 */
const fontReady: Promise<void> = Promise.race([
  document.fonts.load(HANDLE_FONT, 'Riftcaster@.').then(() => undefined),
  new Promise<void>((resolve) => setTimeout(resolve, 3000)),
]).catch(() => undefined);

let measureContext: CanvasRenderingContext2D | null = null;

function measureHandle(text: string): number {
  measureContext ??= document.createElement('canvas').getContext('2d');
  if (measureContext === null) return 0;
  measureContext.font = `${HANDLE_FONT}, sans-serif`;
  return (
    measureContext.measureText(text).width +
    text.length * HANDLE_LETTER_SPACING_EM * HANDLE_FONT_SIZE
  );
}

/** Sized to the longest handle, not the current one, so the plate never resizes while cycling. */
function plateWidth(links: readonly SocialLink[]): number {
  const longest = Math.max(0, ...links.map((link) => measureHandle(link.handle)));
  return Math.round(clamp(ICON_SIZE + ICON_GAP + longest + SIDE_PADDING, MIN_WIDTH, MAX_WIDTH));
}

export function createSocialsVisual(): LowerThirdVisual<LowerThirdSocialsMessage> {
  const root = getElement('socials');
  const plate = getElement('socialsPlate');
  const link = getElement('socialsLink');
  const icon = getElement('socialsIcon');
  const handle = getElement('socialsHandle');
  getElement('socialsGem').innerHTML = gemSvg('socials');

  const reducedMotion = matchMedia('(prefers-reduced-motion: reduce)').matches;

  let links: SocialLink[] = [];
  let shownLinkIndex = -1;

  /** Where the slide is heading: 1 = on screen, 0 = off. */
  let target = 0;
  /** Slide progress, 0 (off) to 1 (on), linear in time; eased when applied. */
  let progress = 0;
  /** When the plate last started showing from fully hidden; the cycle counts from here. */
  let shownAt = 0;
  let lastFrame = 0;
  let frameRequest = 0;

  /** Cancels a show still waiting for the font, if hide() comes first. */
  let showRequest = 0;
  let resolveHidden: (() => void) | null = null;
  let hideFallback = 0;

  function showLink(index: number): void {
    const current = links[index];
    // The path data is a constant from socialIcons.ts; the handle (user input) goes in as text.
    icon.innerHTML = `<svg viewBox="0 0 24 24" width="${ICON_SIZE}" height="${ICON_SIZE}"><path d="${socialIconPaths[current.network]}" /></svg>`;
    handle.textContent = current.handle;
    shownLinkIndex = index;
  }

  function finishHiding(): void {
    clearTimeout(hideFallback);
    cancelAnimationFrame(frameRequest);
    frameRequest = 0;
    progress = 0;
    root.classList.add('is-hidden');
    resolveHidden?.();
    resolveHidden = null;
  }

  function frame(now: number): void {
    const seconds = Math.min(0.1, Math.max(0, (now - lastFrame) / 1000));
    lastFrame = now;

    const duration = reducedMotion ? 0.01 : target === 1 ? SHOW_SECONDS : HIDE_SECONDS;
    progress = clamp(progress + (target === 1 ? seconds : -seconds) / duration);
    root.style.transform = `translateY(${(1 - easeInOutCubic(progress)) * SLIDE_PX}px)`;

    if (progress === 0 && target === 0) {
      finishHiding();
      return;
    }

    // The cycle: one link at a time. Each fades out over the last FADE_SECONDS of its
    // slot, then the next fades in over the first FADE_SECONDS of its own. The first
    // link arrives already visible while the plate slides in.
    const count = links.length;
    const elapsed = Math.max(0, (now - shownAt) / 1000);
    const index = Math.floor(elapsed / SLOT_SECONDS) % count;
    const local = elapsed % SLOT_SECONDS;
    const fadeIn =
      elapsed < SLOT_SECONDS || count < 2 ? 1 : easeInOutCubic(clamp(local / FADE_SECONDS));
    const fadeOut =
      count < 2 ? 0 : easeInOutCubic(clamp((local - (SLOT_SECONDS - FADE_SECONDS)) / FADE_SECONDS));

    if (index !== shownLinkIndex) showLink(index);
    link.style.opacity = String(fadeIn * (1 - fadeOut));

    frameRequest = requestAnimationFrame(frame);
  }

  function start(message: LowerThirdSocialsMessage): void {
    links = message.links.filter((entry) => entry.handle.trim() !== '');
    if (links.length === 0) return; // nothing to cycle through: show nothing

    const width = plateWidth(links);
    root.style.width = `${width}px`;
    root.style.marginLeft = `${-width / 2}px`;
    plate.innerHTML = plateSvg('socials', width, PLATE_HEIGHT);
    showLink(0);
    link.style.opacity = '1';

    const now = performance.now();
    clearTimeout(hideFallback);
    if (progress === 0) shownAt = now; // re-showing mid-exit continues the current cycle
    target = 1;
    lastFrame = now;
    root.classList.remove('is-hidden');
    if (frameRequest === 0) frameRequest = requestAnimationFrame(frame);
  }

  return {
    show(message) {
      const request = ++showRequest;
      void fontReady.then(() => {
        if (request === showRequest) start(message);
      });
    },

    hide() {
      showRequest++; // a show still waiting for the font won't start
      if (progress === 0) {
        finishHiding();
        return Promise.resolve();
      }

      target = 0;
      return new Promise<void>((resolve) => {
        resolveHidden = resolve;
        // requestAnimationFrame doesn't run in a hidden page, so don't rely on it to
        // finish: after the slide's duration, finish regardless.
        hideFallback = window.setTimeout(finishHiding, HIDE_SECONDS * 1000 + 100);
      });
    },
  };
}
