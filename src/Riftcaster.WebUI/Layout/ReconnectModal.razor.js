// Opens, closes and words the reconnection dialog (ReconnectModal.razor) as Blazor reports the
// connection's state. Blazor also sets a class per state on the dialog, which the CSS uses to show
// the right buttons. Based on the .NET 10 Blazor template's ReconnectModal.razor.js.

const dialog = document.getElementById('components-reconnect-modal');
const status = document.getElementById('reconnect-status');
const countdown = document.getElementById('reconnect-countdown');
const retryButton = document.getElementById('reconnect-retry');
const resumeButton = document.getElementById('reconnect-resume');
const reloadButton = document.getElementById('reconnect-reload');
const title = document.getElementById('reconnect-title');
const move = document.getElementById('reconnect-move');
const moveLink = document.getElementById('reconnect-move-link');
const connectionLost = title.textContent;

const messages = {
  reconnecting: 'Reconnecting to the server…',
  retrying: 'Still trying to reconnect to the server…',
  failed: "Couldn't reconnect. Check that the Riftcaster server is running, then try again.",
  paused: 'The server paused this session.',
  resumeFailed: "Couldn't resume the session. Reload the page to continue.",
  rejected: 'The server has restarted. Reloading…',
};

dialog.addEventListener('components-reconnect-state-changed', (event) => {
  const { state, currentAttempt, secondsToNextAttempt } = event.detail;

  if (state === 'hide') {
    // Reconnected: Riftcaster is where it was, so it didn't move (or hasn't yet), and the page stays.
    document.dispatchEvent(new Event('riftcaster-move-cancel'));
    showConnectionLost();
    dialog.close();
    return;
  }

  if (!dialog.open) {
    dialog.showModal();
  }

  // Riftcaster is moving to another port (PortMoveNotice.razor.js): reconnecting to this address can't work,
  // and the page is on its way to the new one, so say that, not that it's trying to reconnect.
  if (document.documentElement.dataset.movingTo) {
    showMoving(document.documentElement.dataset.movingTo, document.documentElement.dataset.movingAddress);
    return;
  }

  switch (state) {
    case 'show':
      setStatus(messages.reconnecting);
      break;
    case 'retrying':
      // Raised for every attempt, and every second while waiting for the next one.
      setStatus(currentAttempt > 1 ? messages.retrying : messages.reconnecting);
      break;
    case 'failed':
      // Blazor has stopped trying by itself.
      setStatus(messages.failed);
      retryButton.focus();
      document.addEventListener('visibilitychange', retryWhenVisible);
      break;
    case 'paused':
      setStatus(messages.paused);
      resumeButton.focus();
      break;
    case 'resume-failed':
      setStatus(messages.resumeFailed);
      reloadButton.focus();
      break;
    case 'rejected':
      // The server is back, but no longer has this page's session (it restarted): load a fresh one.
      setStatus(messages.rejected);
      location.reload();
      break;
  }

  // Between attempts, the countdown; during one, "Trying now", so the line never comes and goes.
  countdown.textContent =
    state !== 'retrying' || currentAttempt <= 1
      ? ''
      : secondsToNextAttempt > 0
        ? `Next attempt in ${secondsToNextAttempt} s.`
        : 'Trying now…';
});

retryButton.addEventListener('click', retry);
resumeButton.addEventListener('click', resume);
reloadButton.addEventListener('click', () => location.reload());

function showMoving(port, address) {
  dialog.classList.add('moving');
  title.textContent = `Riftcaster is moving to port ${port}`;
  setStatus('Taking you to the new address. This takes a few seconds.');
  countdown.textContent = '';
  moveLink.href = address;
  move.hidden = false;
}

function showConnectionLost() {
  dialog.classList.remove('moving');
  title.textContent = connectionLost;
  move.hidden = true;
}

// Only when the words change, so the alert is announced once per state, not on every update.
function setStatus(text) {
  if (status.textContent !== text) {
    status.textContent = text;
  }
}

async function retry() {
  document.removeEventListener('visibilitychange', retryWhenVisible);
  setStatus(messages.reconnecting);

  try {
    // True: reconnected (Blazor then raises "hide"). False: the server is back but no longer has
    // this session. Throws: the server still can't be reached.
    if (!(await Blazor.reconnect())) {
      if (await Blazor.resumeCircuit()) {
        dialog.close();
      } else {
        location.reload();
      }
    }
  } catch {
    setStatus(messages.failed);
    retryButton.focus();
    document.addEventListener('visibilitychange', retryWhenVisible);
  }
}

async function resume() {
  try {
    if (!(await Blazor.resumeCircuit())) {
      location.reload();
    }
  } catch {
    dialog.classList.replace('components-reconnect-paused', 'components-reconnect-resume-failed');
    setStatus(messages.resumeFailed);
    reloadButton.focus();
  }
}

// After giving up, try again as soon as the operator comes back to the tab.
async function retryWhenVisible() {
  if (document.visibilityState === 'visible') {
    await retry();
  }
}
