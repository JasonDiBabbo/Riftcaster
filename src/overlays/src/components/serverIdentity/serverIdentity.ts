import type { ServerIdentity } from '../../generated';

class ServerIdentityComponent {
  constructor(private readonly root: HTMLElement) {}

  render(identity: ServerIdentity): void {
    this.root.textContent = `Server Name: ${identity.name}, Version: ${identity.version}, Start Time: ${new Date(identity.startTime).toLocaleTimeString()}`;
  }
}

const root = document.getElementById('server-identity');

if (!root) {
  console.error('Could not find container element for server identity');
} else {
  new ServerIdentityComponent(root).render({
    name: 'Example Server',
    version: '1.0.0',
    startTime: new Date().toISOString(),
  });
}
