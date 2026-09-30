/**
 * What every lower third visual (one per message type) provides, so lowerThird.ts
 * can sequence them the same way: the outgoing visual's exit finishes before the
 * incoming one's entrance starts, whichever types they are.
 */
export interface LowerThirdVisual<TMessage> {
  /** Puts the message on screen, entering from hidden. */
  show(message: TMessage): void;
  /** Takes the visual off screen. Resolves once it's fully hidden. */
  hide(): Promise<void>;
}

/**
 * Resolves when `property` finishes transitioning on `element` itself (not a
 * descendant), or after `timeoutMs` in case transitionend never fires: a
 * backgrounded tab, prefers-reduced-motion, or a transition that didn't start.
 */
export function waitForTransition(
  element: HTMLElement,
  property: string,
  timeoutMs: number
): Promise<void> {
  return new Promise((resolve) => {
    const finish = () => {
      clearTimeout(timer);
      element.removeEventListener('transitionend', onEnd);
      resolve();
    };
    const onEnd = (event: TransitionEvent) => {
      if (event.target === element && event.propertyName === property) finish();
    };
    const timer = setTimeout(finish, timeoutMs);
    element.addEventListener('transitionend', onEnd);
  });
}

/**
 * Reads a duration custom property (e.g. `--lower-third-exit-duration: 2s`) from
 * :root, so script timings can't drift from the CSS transitions they wait for.
 */
export function cssDurationMs(propertyName: string, fallbackMs: number): number {
  const raw = getComputedStyle(document.documentElement).getPropertyValue(propertyName).trim();
  if (raw.endsWith('ms')) return parseFloat(raw);
  if (raw.endsWith('s')) return parseFloat(raw) * 1000;
  return fallbackMs; // the property was removed or renamed: fall back rather than NaN a timeout
}
