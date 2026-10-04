// @ts-check
/// <reference types="node" />
/**
 * Checks that a published server works on its own (issue #48): starts it from the publish folder,
 * as Production and from a different current folder (as a shortcut or a terminal elsewhere would),
 * then fetches the admin, every overlay page and the REST API's docs page, with the scripts and
 * stylesheets each one links, and the API's OpenAPI document.
 *
 * Also fails if the publish folder contains a data folder: that's the computer's own saved state
 * (the access code, players and so on), which must never be handed out with the server.
 *
 * Its saved state goes to a temporary folder, so checking leaves the publish folder as it was.
 *
 * Usage: node tools/publish/check-publish.mjs <publish folder>
 */
import { spawn } from 'child_process';
import fs from 'fs';
import net from 'net';
import os from 'os';
import path from 'path';

const publishDir = process.argv[2] ? path.resolve(process.argv[2]) : undefined;
const serverDll = publishDir && path.join(publishDir, 'Riftcaster.Server.dll');

if (!publishDir || !serverDll || !fs.existsSync(serverDll)) {
  console.error('Usage: node tools/publish/check-publish.mjs <publish folder>');
  console.error(
    'Publish first: dotnet publish src/Riftcaster.Server --configuration Release --output <publish folder>'
  );
  process.exit(2);
}

/** @type {string[]} */
const failures = [];

if (fs.existsSync(path.join(publishDir, 'data'))) {
  failures.push(
    'The publish folder has a data folder: saved state from the build machine was published.'
  );
}

const overlaysDir = path.join(publishDir, 'overlays');
const overlayPages = fs.existsSync(overlaysDir)
  ? fs
      .readdirSync(overlaysDir, { withFileTypes: true })
      .filter(
        (entry) =>
          entry.isDirectory() &&
          fs.existsSync(path.join(overlaysDir, entry.name, `${entry.name}.html`))
      )
      .map((entry) => `/overlays/${entry.name}/${entry.name}.html`)
  : [];

if (overlayPages.length === 0) {
  failures.push('The publish folder has no overlay pages (overlays/<name>/<name>.html).');
}

const port = await freePort();
const baseUrl = `http://localhost:${port}`;
const workDir = fs.mkdtempSync(path.join(os.tmpdir(), 'riftcaster-publish-check-'));

/** @type {NodeJS.ProcessEnv} */
const env = { ...process.env };
delete env.ASPNETCORE_ENVIRONMENT; // Production, as a published build runs by default
delete env.DOTNET_ENVIRONMENT;

const server = spawn(
  'dotnet',
  [serverDll, '--urls', baseUrl, '--Storage:DataDirectory', path.join(workDir, 'data')],
  { cwd: workDir, env, stdio: ['ignore', 'pipe', 'pipe'] }
);

let log = '';
server.stdout.on('data', (chunk) => (log += chunk));
server.stderr.on('data', (chunk) => (log += chunk));

try {
  await waitForServer();

  for (const page of ['/', ...overlayPages, '/api/docs/']) {
    await checkPage(page);
  }

  const document = await fetch(new URL('/openapi/v1.json', baseUrl));
  if (!document.ok || !document.headers.get('content-type')?.startsWith('application/json')) {
    failures.push(`/openapi/v1.json: ${document.status}`);
  }

  if (/no overlays will be served/i.test(log)) {
    failures.push('The server logged that it serves no overlays.');
  }
} catch (error) {
  failures.push(error instanceof Error ? error.message : String(error));
} finally {
  // Windows won't delete the temporary folder while the server is still running in it.
  if (server.exitCode === null) {
    const exited = new Promise((resolve) => server.once('exit', resolve));
    server.kill();
    await exited;
  }
  fs.rmSync(workDir, { recursive: true, force: true, maxRetries: 5, retryDelay: 200 });
}

if (failures.length > 0) {
  console.error(`\n${failures.length} problem(s) with the published server:`);
  for (const failure of failures) {
    console.error(`  ${failure}`);
  }
  console.error(`\nServer output:\n${log}`);
  process.exit(1);
}

console.log(
  `The published server serves the admin, ${overlayPages.length} overlays and the API docs, with their scripts and stylesheets.`
);

/**
 * Fetches an HTML page, then the stylesheets and scripts it links to.
 * @param {string} pagePath
 */
async function checkPage(pagePath) {
  const response = await fetch(new URL(pagePath, baseUrl));
  if (!response.ok || !response.headers.get('content-type')?.startsWith('text/html')) {
    failures.push(
      `${pagePath}: ${response.status} ${response.headers.get('content-type') ?? ''}`.trim()
    );
    return;
  }

  const html = await response.text();
  for (const match of html.matchAll(/<link [^>]*href="([^"]+)"|<script [^>]*src="([^"]+)"/g)) {
    const link = new URL(match[1] ?? match[2], new URL(pagePath, baseUrl));
    if (link.origin !== baseUrl) {
      continue; // Nothing outside the server is linked today, but it wouldn't be ours to check
    }
    const linked = await fetch(link);
    if (!linked.ok) {
      failures.push(`${link.pathname} (linked from ${pagePath}): ${linked.status}`);
    }
  }
}

/** Waits up to 30 seconds for the server to answer, or for it to exit. */
async function waitForServer() {
  const deadline = Date.now() + 30_000;
  while (Date.now() < deadline) {
    if (server.exitCode !== null) {
      throw new Error(`The server exited on startup with code ${server.exitCode}.`);
    }
    try {
      await fetch(new URL('/api/info', baseUrl));
      return;
    } catch {
      await new Promise((resolve) => setTimeout(resolve, 250));
    }
  }
  throw new Error('The server did not start within 30 seconds.');
}

/** A port nothing is listening on. */
function freePort() {
  return new Promise((resolve, reject) => {
    const probe = net.createServer();
    probe.once('error', reject);
    probe.listen(0, 'localhost', () => {
      const address = probe.address();
      probe.close(() => resolve(typeof address === 'object' && address ? address.port : 0));
    });
  });
}
