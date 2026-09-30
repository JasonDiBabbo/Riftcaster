/**
 * The keyword lower third: a keyword parallelogram over a description
 * parallelogram, ported from the reviewed keyword lower-third mockup (see
 * keyword.css's header comment).
 */
import { getElement } from '../../shared/dom';
import type { LowerThirdKeywordMessage } from '../../generated';
import { cssDurationMs, waitForTransition, type LowerThirdVisual } from './visual';

export function createKeywordVisual(): LowerThirdVisual<LowerThirdKeywordMessage> {
  const composition = getElement('composition');
  const kwText = getElement('kwText');
  const descText = getElement('descText');

  // composition first - its fade-in is the single shared opacity animation;
  // the rest are each shape's own independent unveil/slide motion.
  const entranceEls = [
    composition,
    getElement('kwShape'),
    getElement('descShape'),
    getElement('descBottomEdge'),
    getElement('descDiamond'),
  ];

  /** Force a CSS animation to (re)start even if its class is already present. */
  function restartAnimation(el: HTMLElement): void {
    el.classList.remove('animate-in');
    void el.offsetWidth; // force reflow
    el.classList.add('animate-in');
  }

  return {
    show(message) {
      kwText.textContent = message.keyword;
      descText.textContent = message.description;
      composition.classList.remove('is-hidden');
      entranceEls.forEach(restartAnimation);
    },

    hide() {
      entranceEls.forEach((el) => el.classList.remove('animate-in'));

      // compositionFadeIn's `both` fill-mode keeps holding opacity:1 even after
      // the entrance animation finishes - removing .animate-in above releases
      // that hold, but without a forced reflow in between, adding .is-hidden
      // happens in the same synchronous style pass, so the browser never
      // settles on the released (plain opacity:1) value as a starting point and
      // the opacity transition below never triggers: it just snaps straight to
      // 0 instead of fading. See keyword.css's .composition/.animate-in.
      void composition.offsetWidth;

      composition.classList.add('is-hidden');
      return waitForTransition(
        composition,
        'opacity',
        cssDurationMs('--lower-third-exit-duration', 2000) + 50
      );
    },
  };
}
