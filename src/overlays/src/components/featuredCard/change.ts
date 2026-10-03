/**
 * What the featured card overlay does when a state arrives:
 *
 * - `none`: it's the card already shown (or on its way in), as on every reconnect.
 * - `hide`: nothing is featured any more; fade out.
 * - `swap`: a different card while one is on screen; fade it out, then the new one in.
 * - `show`: a card while nothing is on screen; fade it straight in.
 *
 * @param currentId The id of the card shown or on its way in, or null for none.
 * @param nextId The id of the card in the new state, or null for none.
 * @param visible Whether a card is on screen now (not mid-fade-out).
 */
export function changeFor(
  currentId: string | null,
  nextId: string | null,
  visible: boolean
): 'none' | 'hide' | 'swap' | 'show' {
  if (nextId === currentId) return 'none';
  if (nextId === null) return 'hide';
  return visible ? 'swap' : 'show';
}
