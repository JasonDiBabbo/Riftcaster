/**
 * TEMPORARY Information lower third: plain text with a simple fade, until it
 * has a real design (issue #23). See information.css.
 */
import { getElement } from '../../shared/dom';
import type { LowerThirdInformationMessage } from '../../generated';
import { cssDurationMs, waitForTransition, type LowerThirdVisual } from './visual';

export function createInformationVisual(): LowerThirdVisual<LowerThirdInformationMessage> {
  const info = getElement('info');
  const infoText = getElement('infoText');

  return {
    show(message) {
      infoText.textContent = message.message;
      info.classList.remove('is-hidden');
    },

    hide() {
      info.classList.add('is-hidden');
      return waitForTransition(info, 'opacity', cssDurationMs('--info-fade-duration', 400) + 50);
    },
  };
}
