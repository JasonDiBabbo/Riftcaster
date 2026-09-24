import type { ServerIdentity } from '../../generated';

class ServerIdentityComponent {
  constructor(private readonly root: HTMLElement) {}

  render(identity: ServerIdentity): void {
    this.root.textContent = `Server Name: ${identity.name}, Version: ${identity.version}, Start Time: ${new Date(identity.startTime).toLocaleTimeString()}`;
  }
}

async function load(root: HTMLElement): Promise<void> {
  try {
    const response = await fetch('/api/info');
    if (!response.ok) {
      throw new Error(`HTTP ${response.status} ${response.statusText}`);
    }
    const identity: ServerIdentity = await response.json();
    new ServerIdentityComponent(root).render(identity);
  } catch (err) {
    console.error('Error rendering server identity:', err);
  }
}

const root = document.getElementById('server-identity');
if (!root) {
  console.error('Could not find container element for server identity');
} else {
  void load(root);
}
