/**
 * Lower third overlay. Shows whatever the admin panel puts on air, streamed
 * from the server over Server-Sent Events (GET /api/lower-third/events): the
 * current state on connect, then one event per change.
 *
 * Each message type has its own visual (keywordVisual.ts, informationVisual.ts,
 * socialsVisual.ts), with its own entrance and exit. This file only decides which visual shows
 * what, and sequences them: whatever is on screen finishes its exit before the
 * next message enters, even when the two are different types.
 */
import type { LowerThirdMessage, LowerThirdState } from '../../generated';
import { createInformationVisual } from './informationVisual';
import { createKeywordVisual } from './keywordVisual';
import { createSocialsVisual } from './socialsVisual';
import type { LowerThirdVisual } from './visual';

// Module scripts run after the document is parsed, so the elements exist here.
const keyword = createKeywordVisual();
const information = createInformationVisual();
const socials = createSocialsVisual();

/**
 * The message currently shown (or being moved toward), as JSON; null when
 * nothing is showing. render() compares against it so that an event carrying
 * the same message, as the first event after every reconnect does, doesn't
 * replay the entrance animation. Compared by content rather than by id so an
 * edit to the live message still counts as a change.
 */
let currentKey: string | null = null;

/** The visual on screen (or entering), if any. Only its hide() is needed later. */
let onScreen: Pick<LowerThirdVisual<unknown>, 'hide'> | null = null;

/** Completes when the most recent exit has finished; nothing enters before then. */
let exiting: Promise<void> = Promise.resolve();

/** Increments on every change, so a render that was waiting on an exit can tell it's been superseded. */
let generation = 0;

/** Starts the visual for this message's type, and returns it; null for a type this build doesn't know. */
function show(message: LowerThirdMessage): Pick<LowerThirdVisual<unknown>, 'hide'> | null {
  switch (message.type) {
    case 'keyword':
      keyword.show(message);
      return keyword;
    case 'information':
      information.show(message);
      return information;
    case 'socials':
      socials.show(message);
      return socials;
    default: {
      // Compile time: adding a message type to Contracts makes this assignment
      // fail until the type gets a case above. Runtime: a type this build
      // doesn't know (e.g. a newer server) shows nothing instead of breaking.
      const unhandled: never = message;
      console.warn(`No overlay for lower third type "${(unhandled as { type: string }).type}"`);
      return null;
    }
  }
}

async function render(state: LowerThirdState): Promise<void> {
  const message = state.message;
  const key = message === null ? null : JSON.stringify(message);
  if (key === currentKey) return; // same message, e.g. the first event after a reconnect
  currentKey = key;
  const thisRender = ++generation;

  if (onScreen !== null) {
    exiting = onScreen.hide();
    onScreen = null;
  }

  // Wait for any exit in progress, including one a previous render started.
  await exiting;
  if (thisRender !== generation || message === null) return; // superseded, or nothing to show

  onScreen = show(message);
}

// EventSource reconnects by itself (every few seconds) whenever the stream
// drops, e.g. across a server restart; the first event after reconnecting is
// the current state, which render() de-duplicates via currentKey.
const source = new EventSource('/api/lower-third/events');

source.onmessage = (event: MessageEvent<string>) => {
  void render(JSON.parse(event.data) as LowerThirdState);
};

source.onerror = () => {
  console.warn('Lower third stream interrupted; the browser will reconnect automatically.');
};
