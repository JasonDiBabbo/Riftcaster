/**
 * Typed accessor for document.getElementById - fails with a clear error message
 */
export function getElement<T extends HTMLElement = HTMLElement>(id: string): T {
  const el = document.getElementById(id);

  if (el === null) {
    throw new Error(`DOM element with ID '${id}' was not found.`);
  }

  return el as T;
}
