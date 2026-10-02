/**
 * Subscribes to one of the server's state streams (server-sent events), calling onState with each
 * state it sends: the current state as soon as it connects, then one per change.
 *
 * EventSource reconnects by itself (every few seconds) whenever the stream drops, e.g. across a
 * server restart, and the first event after reconnecting is the current state again. So onState
 * can be called with a state it has already shown: overlays compare by content before animating.
 *
 * @param url The stream, e.g. '/api/timer/events'.
 * @param name What to call the stream in console messages, e.g. 'Timer'.
 * @param onState Called with each state, parsed from JSON and typed by the generated contracts.
 */
export function subscribe<T>(url: string, name: string, onState: (state: T) => void): void {
  const source = new EventSource(url);

  source.onmessage = (event: MessageEvent<string>) => {
    onState(JSON.parse(event.data) as T);
  };

  source.onerror = () => {
    console.warn(`${name} stream interrupted; the browser will reconnect automatically.`);
  };
}
