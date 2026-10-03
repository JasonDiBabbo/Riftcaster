import type { Card } from '../generated';

/**
 * Shows a card's image, or hides the image when there's no card. Used by the per-player image
 * overlays, which follow the whole match stream: the same card arrives again with every score
 * change, so the image is only reloaded when the card's image actually changes.
 */
export function showCardImage(image: HTMLImageElement, card: Card | null | undefined): void {
  if (!card) {
    image.hidden = true;
    image.removeAttribute('src');
    image.alt = '';
    return;
  }

  if (image.getAttribute('src') !== card.imageUrl) {
    image.src = card.imageUrl;
  }

  image.alt = card.name;
  image.hidden = false;
}
