/**
 * Featured card overlay. Shows the card the admin features, streamed from the server
 * (/api/featured-card/events): the current state on connect, then one per change.
 *
 * Switching cards fades the old one out before the new one fades in, rather than cutting to it
 * under a still-visible image. Ported from the first version of this application
 * (app/components/featuredCard/featuredCard.ts), which keyed cards by collector code; this one
 * uses the card's id, since a card and its metal promo share a code.
 */
import type { Card, FeaturedCardState } from '../../generated';
import { getElement } from '../../shared/dom';
import { subscribe } from '../../shared/stream';
import { changeFor } from './change';

const image = getElement<HTMLImageElement>('image');

/** The id of the card shown or on its way in; null when nothing is featured. */
let currentId: string | null = null;

/**
 * How long the fade takes: read from the custom property featuredCard.css's transition uses, so
 * the two can't drift apart.
 */
function fadeDurationMs(): number {
  const raw = getComputedStyle(document.documentElement)
    .getPropertyValue('--featured-card-fade-duration')
    .trim();
  if (raw.endsWith('ms')) return parseFloat(raw);
  if (raw.endsWith('s')) return parseFloat(raw) * 1000;
  return 1000; // The property was removed or renamed: fall back rather than wait NaN ms
}

/**
 * Loads the card's image, then fades it in, so it doesn't appear half-drawn. Gives up if another
 * change came along meanwhile, or if the image can't be loaded (rather than showing a broken one).
 */
function show(card: Card): void {
  image.src = card.imageUrl;
  image.alt = card.name;
  image.decode().then(
    () => {
      if (currentId === card.id) image.classList.add('visible');
    },
    () =>
      console.warn(`Featured card: couldn't load the image for ${card.name} (${card.imageUrl}).`)
  );
}

/** Fades the current card out, then shows the new one, unless another change came along meanwhile. */
function swap(card: Card): void {
  let done = false;
  const proceed = () => {
    if (done) return;
    done = true;
    if (currentId === card.id) show(card);
  };

  image.addEventListener(
    'transitionend',
    (event) => {
      if (event.propertyName === 'opacity') proceed();
    },
    { once: true }
  );
  // In case transitionend never fires (e.g. OBS suspended the transition while the source was
  // hidden): swap at about the time the fade would have finished.
  setTimeout(proceed, fadeDurationMs() + 50);

  image.classList.remove('visible');
}

function render(state: FeaturedCardState): void {
  const card = state.card;
  const change = changeFor(currentId, card?.id ?? null, image.classList.contains('visible'));
  currentId = card?.id ?? null;

  switch (change) {
    case 'hide':
      image.classList.remove('visible');
      break;
    case 'swap':
      swap(card!);
      break;
    case 'show':
      show(card!);
      break;
  }
}

subscribe<FeaturedCardState>('/api/featured-card/events', 'Featured card', render);
