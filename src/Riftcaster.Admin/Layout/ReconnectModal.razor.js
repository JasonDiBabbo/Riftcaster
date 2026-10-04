// Opens, closes and words the reconnection dialog (ReconnectModal.razor) as Blazor reports the
// connection's state. Blazor also sets a class per state on the dialog, which the CSS uses to show
// the right buttons. Based on the .NET 10 Blazor template's ReconnectModal.razor.js.

const dialog = document.getElementById('components-reconnect-modal');
const status = document.getElementById('reconnect-status');
const countdown = document.getElementById('reconnect-countdown');
const retryButton = document.getElementById('reconnect-retry');
const resumeButton = document.getElementById('reconnect-resume');
const reloadButton = document.getElementById('reconnect-reload');

const messages = {
  reconnecting: 'Reconnecting to the server…',
  retrying: 'Still trying to reconnect to the server…',
  failed: 'Couldn’t reconnect. Check that the Riftcaster server is running, then try again.',
  paused: 'The server paused this session.',
  resumeFailed: 'Couldn’t resume the session. Reload the page to carry on.',
  rejected: 'The server has restarted. Reloading…',
};

dialog.addEventListener('components-reconnect-state-changed', (event) => {
  const { state, currentAttempt, secondsToNextAttempt } = event.detail;

  if (state === 'hide') {
    dialog.close();
    return;
  }

  if (!dialog.open) {
    dialog.showModal();
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
