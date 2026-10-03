import { describe, expect, it } from 'vitest';
import { changeFor } from './change';

describe('changeFor', () => {
  it('does nothing for the card already shown, as after a reconnect', () => {
    expect(changeFor('a', 'a', true)).toBe('none');
  });

  it('does nothing when nothing was featured and still is', () => {
    expect(changeFor(null, null, false)).toBe('none');
  });

  it('hides when the card is cleared', () => {
    expect(changeFor('a', null, true)).toBe('hide');
  });

  it('shows a card straight away when nothing is on screen', () => {
    expect(changeFor(null, 'a', false)).toBe('show');
  });

  it('swaps a card that is on screen for another', () => {
    expect(changeFor('a', 'b', true)).toBe('swap');
  });

  it('shows a card straight away when the previous one is already fading out', () => {
    // b was featured while a faded out; c arrives before b appeared, so there's nothing left to fade.
    expect(changeFor('b', 'c', false)).toBe('show');
  });
});
