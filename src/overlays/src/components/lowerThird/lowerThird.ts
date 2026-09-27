/**
 * Lower third overlay. Shows whatever the admin panel puts on air, streamed
 * from the server over Server-Sent Events (GET /api/lower-third/events): the
 * current state on connect, then one event per change. Only the keyword type
 * has a visual (ported from the reviewed keyword lower-third mockup; see
 * lowerThird.css's header comment).
 */
import { getElement } from '../../shared/dom';
import type { LowerThirdKeywordMessage, LowerThirdState } from '../../generated';

/**
 * The message currently shown (or being faded toward), as JSON; null when
 * nothing is showing. render() compares against it so that an event carrying
 * the same message, as the first event after every reconnect does, doesn't
 * replay the entrance animation. Compared by content rather than by id so an
 * edit to the live message still counts as a change.
 */
let currentKey: string | null = null;

interface LowerThirdElements {
  composition: HTMLElement;
  kwShape: HTMLElement;
  kwText: HTMLElement;
  descShape: HTMLElement;
  descText: HTMLElement;
  descBottomEdge: HTMLElement;
  descDiamond: HTMLElement;
}

function getElements(): LowerThirdElements {
  return {
    composition: getElement('composition'),
    kwShape: getElement('kwShape'),
    kwText: getElement('kwText'),
    descShape: getElement('descShape'),
    descText: getElement('descText'),
    descBottomEdge: getElement('descBottomEdge'),
    descDiamond: getElement('descDiamond'),
  };
}

/** How long the exit (fade-out) transition takes - read from the same custom
 * property lowerThird.css's transition uses, so the two can't drift apart.
 * Also what a content swap (fadeOutThenApply below) times itself against.
 */
function getExitDurationMs(): number {
  const raw = getComputedStyle(document.documentElement)
    .getPropertyValue('--lower-third-exit-duration')
    .trim();
  if (raw.endsWith('ms')) return parseFloat(raw);
  if (raw.endsWith('s')) return parseFloat(raw) * 1000;
  return 2000; // the property was removed/renamed - fall back rather than NaN a timeout
}

// composition first - its fade-in is the single shared opacity animation;
// the rest are each shape's own independent unveil/slide motion.
function entranceEls(els: LowerThirdElements): HTMLElement[] {
  return [els.composition, els.kwShape, els.descShape, els.descBottomEdge, els.descDiamond];
}

/** Force a CSS animation to (re)start even if its class is already present. */
function restartAnimation(el: HTMLElement): void {
  el.classList.remove('animate-in');
  void el.offsetWidth; // force reflow
  el.classList.add('animate-in');
}

function playEntrance(els: LowerThirdElements): void {
  els.composition.classList.remove('is-hidden');
  entranceEls(els).forEach(restartAnimation);
}

function playExit(els: LowerThirdElements): void {
  entranceEls(els).forEach((el) => el.classList.remove('animate-in'));

  // compositionFadeIn's `both` fill-mode keeps holding opacity:1 even after
  // the entrance animation finishes - removing .animate-in above releases
  // that hold, but without a forced reflow in between, adding .is-hidden
  // happens in the same synchronous style pass, so the browser never
  // settles on the released (plain opacity:1) value as a starting point and
  // the opacity transition below never triggers: it just snaps straight to
  // 0 instead of fading. See lowerThird.css's .composition/.animate-in.
  void els.composition.offsetWidth;

  els.composition.classList.add('is-hidden');
}

function applyMessage(els: LowerThirdElements, message: LowerThirdKeywordMessage): void {
  els.kwText.textContent = message.keyword;
  els.descText.textContent = message.description;
  playEntrance(els);
}

/**
 * Swap to a new message only after the current one has finished fading out,
 * so an operator showing a different message while one is already up reads
 * as "old one fades out, then the new one fades in" rather than an instant
 * cut underneath a still-opaque composition. Mirrors featuredCard.ts's
 * fadeOutThenApply.
 * @param expectedKey - bail if a newer change already moved past this one
 *   while the fade-out was in progress
 */
function fadeOutThenApply(
  els: LowerThirdElements,
  message: LowerThirdKeywordMessage,
  expectedKey: string
): void {
  let done = false;
  const proceed = () => {
    if (done) return;
    done = true;
    if (currentKey === expectedKey) applyMessage(els, message);
  };

  els.composition.addEventListener(
    'transitionend',
    (e) => {
      if (e.propertyName === 'opacity') proceed();
    },
    { once: true }
  );
  // Fallback in case transitionend never fires (e.g. the tab was
  // backgrounded, or prefers-reduced-motion suppressed the transition
  // entirely) - the swap still happens close to when the fade-out would
  // have finished.
  setTimeout(proceed, getExitDurationMs() + 50);

  playExit(els);
}

function render(state: LowerThirdState, els: LowerThirdElements): void {
  const message = state.message;

  if (message === null) {
    if (currentKey === null) return; // already hidden
    currentKey = null;
    playExit(els);
    return;
  }

  const key = JSON.stringify(message);
  if (key === currentKey) return; // same message, e.g. the first event after a reconnect
  currentKey = key;

  switch (message.type) {
    case 'keyword':
      if (els.composition.classList.contains('is-hidden')) {
        // Nothing on screen to fade out first - go straight to the new message.
        applyMessage(els, message);
      } else {
        // Something else is already showing - fade it out first, then swap.
        fadeOutThenApply(els, message, key);
      }
      break;
    default: {
      // Compile time: adding a message type to Contracts makes this assignment
      // fail until the type gets a case above. Runtime: a type this build
      // doesn't know (e.g. a newer server) hides the overlay instead of breaking.
      const unhandled: never = message.type;
      console.warn(`No overlay for lower third type "${String(unhandled)}" - hiding`);
      playExit(els);
    }
  }
}

// Module scripts run after the document is parsed, so the elements exist here.
const els = getElements();

// EventSource reconnects by itself (every few seconds) whenever the stream
// drops, e.g. across a server restart; the first event after reconnecting is
// the current state, which render() de-duplicates via currentKey.
const source = new EventSource('/api/lower-third/events');

source.onmessage = (event: MessageEvent<string>) => {
  render(JSON.parse(event.data) as LowerThirdState, els);
};

source.onerror = () => {
  console.warn('Lower third stream interrupted; the browser will reconnect automatically.');
};
