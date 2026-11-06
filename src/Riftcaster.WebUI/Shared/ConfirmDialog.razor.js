// Makes the dialog (ConfirmDialog.razor) modal: the rest of the page can't be used until it's answered.

export function show(dialog, cancel) {
  // It was rendered open, so that it shows even if this script can't run; a modal one has to be opened by script.
  if (dialog.open) {
    dialog.close();
  }

  dialog.showModal();
  cancel.focus();

  // Escape is the cancel button, so that it's answered the way the button answers it.
  dialog.addEventListener('cancel', (event) => {
    event.preventDefault();
    cancel.click();
  });
}
