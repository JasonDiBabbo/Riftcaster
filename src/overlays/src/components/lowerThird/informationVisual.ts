/**
 * The Information lower third: the message on the gilded plate the Socials lower third
 * uses, sliding up from below the frame. Styles: information.css. Plate artwork: plate.ts.
 *
 * The CSS sizes the box to the message (one line, or two once it reaches the maximum
 * width); show() then draws the plate at whatever size that came to.
 */
import { getElement } from '../../shared/dom';
import type { LowerThirdInformationMessage } from '../../generated';
import { gemSvg, plateSvg } from './plate';
import { cssDurationMs, waitForTransition, type LowerThirdVisual } from './visual';

// Must match information.css's .info__text.
const MESSAGE_FONT = "600 27px 'Roboto Condensed Variable'";

/**
 * Resolves once the message font has loaded (or after 3s, so a font that never arrives
 * can't block the overlay). The plate is sized from the laid-out text; measuring it in a
 * fallback font would size the plate wrongly.
 */
const fontReady: Promise<void> = Promise.race([
  document.fonts.load(MESSAGE_FONT, 'Riftcaster').then(() => undefined),
  new Promise<void>((resolve) => setTimeout(resolve, 3000)),
]).catch(() => undefined);

export function createInformationVisual(): LowerThirdVisual<LowerThirdInformationMessage> {
  const root = getElement('info');
  const plate = getElement('infoPlate');
  const text = getElement('infoText');
  getElement('infoGem').innerHTML = gemSvg('info');

  /** Increments on every show and hide, so a stale one (waiting on the font or a slide) does nothing. */
  let request = 0;

  return {
    show(message) {
      const thisRequest = ++request;
      void fontReady.then(() => {
        if (thisRequest !== request) return;

        text.textContent = message.message;
        root.classList.remove('is-hidden'); // laid out, still below the frame
        const width = root.offsetWidth;
        const height = root.offsetHeight;
        plate.innerHTML = plateSvg('info', width, height);

        void root.offsetWidth; // start the slide from off screen, not from display: none
        root.classList.remove('is-off-screen');
      });
    },

    async hide() {
      const thisRequest = ++request;
      if (root.classList.contains('is-hidden')) return;

      root.classList.add('is-off-screen');
      await waitForTransition(root, 'transform', cssDurationMs('--info-hide-duration', 600) + 50);
      if (thisRequest === request) root.classList.add('is-hidden');
    },
  };
}
