/**
 * Subscribes to one of the server's state streams, calling onState with each state it sends: the
 * current state as soon as it connects, then one per change.
 *
 * The streams are WebSockets, one JSON message per state. (Not server-sent events: over plain
 * HTTP a browser opens at most 6 connections to one host, and OBS shares one browser across all
 * its sources, so a 7th overlay would never connect. See issue #32.)
 *
 * When the connection drops, e.g. across a server restart, this reconnects by itself, waiting a
 * little longer after each failed attempt. The first message after reconnecting is the current
 * state again, so onState can be called with a state it has already shown: overlays compare by
 * content before animating.
 *
 * @param path The stream, e.g. '/api/timer/events'.
 * @param name What to call the stream in console messages, e.g. 'Timer'.
 * @param onState Called with each state, parsed from JSON and typed by the generated contracts.
 */
export function subscribe<T>(path: string, name: string, onState: (state: T) => void): void {
  const url = socketUrl(path, window.location.href);
  let attempt = 0;

  function connect(): void {
    const socket = new WebSocket(url);

    socket.onopen = () => {
      attempt = 0;
    };

    socket.onmessage = (event: MessageEvent<string>) => {
      onState(JSON.parse(event.data) as T);
    };

    // Also follows an error: a failed attempt closes too.
    socket.onclose = () => {
      const delay = reconnectDelay(attempt++);
      console.warn(`${name} connection lost; reconnecting in ${delay / 1000}s.`);
      setTimeout(connect, delay);
    };
  }

  connect();
}

/** The WebSocket address for a server path, on the same host as the page: ws://, or wss:// over HTTPS. */
export function socketUrl(path: string, pageUrl: string): string {
  const url = new URL(path, pageUrl);
  url.protocol = url.protocol === 'https:' ? 'wss:' : 'ws:';
  return url.href;
}

/**
 * How long to wait before reconnect attempt `attempt` (from 0): 1s, then doubling to at most 10s,
 * so a restarting server is picked up quickly without a stopped one being hammered.
 */
export function reconnectDelay(attempt: number): number {
  return Math.min(1000 * 2 ** attempt, 10_000);
}
