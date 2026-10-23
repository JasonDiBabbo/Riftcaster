// Takes the page to Riftcaster's new port when it moves there (PortMoveNotice.razor). It runs in the page, not
// through the connection to the server, because that connection closes with the old port.
//
// Waits until the new port answers /api/info with this server's start time, which is how it knows it's
// Riftcaster, and the same one: another program might be on that port, and it mustn't take the page there.
// The server lets a page from the same host on another port read that answer.

let timer = 0;

export function follow(port, startTime) {
  cancel();

  const target = `${location.protocol}//${location.hostname}:${port}`;
  const destination = target + location.pathname + location.search;
  const started = Date.parse(startTime);
  const giveUpAt = Date.now() + 90_000;

  // The reconnection dialog words itself from these, since it's what the page shows once the connection is lost.
  document.documentElement.dataset.movingTo = String(port);
  document.documentElement.dataset.movingAddress = destination;

  const check = async () => {
    try {
      const response = await fetch(`${target}/api/info`, { cache: 'no-store' });
      if (response.ok) {
        const info = await response.json();
        // To the millisecond, near enough: nothing else has this start time.
        if (Math.abs(Date.parse(info.startTime) - started) < 5) {
          location.href = destination;
          return;
        }
      }
    } catch {
      // Not up yet.
    }

    if (Date.now() < giveUpAt) {
      timer = setTimeout(check, 500);
    }
  };

  timer = setTimeout(check, 500);
}

export function cancel() {
  clearTimeout(timer);
  timer = 0;
  delete document.documentElement.dataset.movingTo;
  delete document.documentElement.dataset.movingAddress;
}

// The server is back on the port the page is on (the move didn't happen): stay.
document.addEventListener('riftcaster-move-cancel', cancel);
