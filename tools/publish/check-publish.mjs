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
 * A self-contained build (a release, issue #60) is started through its own Riftcaster.Server.exe,
 * as someone who downloaded it would, and must carry its own .NET runtime. Any other publish is
 * started with `dotnet Riftcaster.Server.dll`. With --version, the server must report that version.
 *
 * A Windows release also has the tray launcher, Riftcaster.exe (#68), which people start. It runs
 * the same server, so the checks run again with the server started by it. The launcher allows one
 * copy at a time, so close any Riftcaster already running on this computer first.
 *
 * Usage: node tools/publish/check-publish.mjs <publish folder> [--version <version>]
 */
import { spawn } from 'child_process';
import fs from 'fs';
import net from 'net';
import os from 'os';
import path from 'path';

const publishDir = process.argv[2] ? path.resolve(process.argv[2]) : undefined;
const serverDll = publishDir && path.join(publishDir, 'Riftcaster.Server.dll');
const versionArg = process.argv.indexOf('--version');
const expectedVersion = versionArg > 0 ? process.argv[versionArg + 1] : undefined;

if (!publishDir || !serverDll || !fs.existsSync(serverDll)) {
  console.error(
    'Usage: node tools/publish/check-publish.mjs <publish folder> [--version <version>]'
  );
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

// A self-contained build has the app's own launcher, and its runtime settings list the frameworks it
// carries; a framework-dependent one needs .NET installed.
const serverExe = path.join(
  publishDir,
  process.platform === 'win32' ? 'Riftcaster.Server.exe' : 'Riftcaster.Server'
);
const selfContained = fs.existsSync(serverExe);
if (selfContained) {
  const runtimeConfig = JSON.parse(
    fs.readFileSync(path.join(publishDir, 'Riftcaster.Server.runtimeconfig.json'), 'utf8')
  );
  if (!runtimeConfig.runtimeOptions?.includedFrameworks) {
    failures.push(
      'The publish folder has Riftcaster.Server.exe but no runtime of its own: it needs .NET installed.'
    );
  }
}

// A Windows release also has the tray launcher (#68), Riftcaster.exe, which is what people start, so
// it's started too. It runs the same server, with WinForms' runtime (Windows Desktop) as well.
const launcherExe = path.join(publishDir, 'Riftcaster.exe');
const launcher = process.platform === 'win32' && fs.existsSync(launcherExe);
if (launcher) {
  const runtimeConfig = JSON.parse(
    fs.readFileSync(path.join(publishDir, 'Riftcaster.runtimeconfig.json'), 'utf8')
  );
  const frameworks = (runtimeConfig.runtimeOptions?.includedFrameworks ?? []).map(
    (/** @type {{ name: string }} */ framework) => framework.name
  );
  if (!frameworks.includes('Microsoft.WindowsDesktop.App')) {
    failures.push(
      'Riftcaster.exe has no Windows Desktop runtime of its own: it needs .NET installed.'
    );
  }
}

/** @type {{ name: string, command: string, args: string[] }[]} */
const entryPoints = [
  selfContained
    ? { name: 'Riftcaster.Server.exe', command: serverExe, args: [] }
    : {
        name: 'dotnet Riftcaster.Server.dll',
        command: 'dotnet',
        args: [serverDll],
      },
];
if (launcher) {
  entryPoints.push({
    name: 'the tray launcher, Riftcaster.exe',
    command: launcherExe,
    // Otherwise every check opens the dashboard in a browser.
    args: ['--Launcher:OpenDashboard=false'],
  });
}

/** @type {string[]} */
const logs = [];
for (const entryPoint of entryPoints) {
  await checkStartedBy(entryPoint);
}

if (failures.length > 0) {
  console.error(`\n${failures.length} problem(s) with the published server:`);
  for (const failure of failures) {
    console.error(`  ${failure}`);
  }
  console.error(`\n${logs.join('\n')}`);
  process.exit(1);
}

console.log(
  `The published server, started by ${entryPoints.map((entryPoint) => entryPoint.name).join(' and by ')}, serves the admin, ${overlayPages.length} overlays and the API docs, with their scripts and stylesheets.`
);

/**
 * Starts the server through one of the publish folder's entry points, checks what it serves, and
 * stops it. Problems go in failures, with the entry point's name.
 * @param {{ name: string, command: string, args: string[] }} entryPoint
 */
async function checkStartedBy(entryPoint) {
  const port = await freePort();
  const baseUrl = `http://localhost:${port}`;
  const workDir = fs.mkdtempSync(path.join(os.tmpdir(), 'riftcaster-publish-check-'));
  const dataDir = path.join(workDir, 'data');

  /** @type {NodeJS.ProcessEnv} */
  const env = { ...process.env };
  delete env.ASPNETCORE_ENVIRONMENT; // Production, as a published build runs by default
  delete env.DOTNET_ENVIRONMENT;

  const server = spawn(
    entryPoint.command,
    [...entryPoint.args, '--urls', baseUrl, '--Storage:DataDirectory', dataDir],
    { cwd: workDir, env, stdio: ['ignore', 'pipe', 'pipe'] }
  );

  // The console's output; the launcher has no console, so its log files are read as well.
  let output = '';
  server.stdout.on('data', (chunk) => (output += chunk));
  server.stderr.on('data', (chunk) => (output += chunk));

  const failuresBefore = failures.length;
  try {
    await waitForServer(server, baseUrl);

    if (expectedVersion) {
      const info = await (await fetch(new URL('/api/info', baseUrl))).json();
      if (info.version !== expectedVersion) {
        failures.push(`The server reports version ${info.version}, not ${expectedVersion}.`);
      }
    }

    for (const page of ['/', ...overlayPages, '/api/docs/']) {
      await checkPage(baseUrl, page);
    }

    const document = await fetch(new URL('/openapi/v1.json', baseUrl));
    if (!document.ok || !document.headers.get('content-type')?.startsWith('application/json')) {
      failures.push(`/openapi/v1.json: ${document.status}`);
    }
  } catch (error) {
    failures.push(error instanceof Error ? error.message : String(error));
    if (entryPoint.command === launcherExe && server.exitCode === 0) {
      failures.push('Is Riftcaster already running? The launcher opens its dashboard instead.');
    }
  } finally {
    // Windows won't delete the temporary folder while the server is still running in it.
    if (server.exitCode === null) {
      const exited = new Promise((resolve) => server.once('exit', resolve));
      server.kill();
      await exited;
    }

    const logFolder = path.join(dataDir, 'logs');
    const logFiles = fs.existsSync(logFolder) ? fs.readdirSync(logFolder) : [];
    const log =
      output + logFiles.map((file) => fs.readFileSync(path.join(logFolder, file), 'utf8')).join('');
    if (/no overlays will be served/i.test(log)) {
      failures.push('The server logged that it serves no overlays.');
    }

    for (let i = failuresBefore; i < failures.length; i++) {
      failures[i] = `${entryPoint.name}: ${failures[i]}`;
    }
    logs.push(`Output of ${entryPoint.name}:\n${log}`);

    // Best effort: a process the server started, such as a browser opened on its dashboard, can
    // keep the folder in use, and that mustn't hide what the check found.
    try {
      fs.rmSync(workDir, { recursive: true, force: true, maxRetries: 5, retryDelay: 200 });
    } catch {
      console.warn(`Couldn't delete the temporary folder ${workDir}; delete it by hand.`);
    }
  }
}

/**
 * Fetches an HTML page, then the stylesheets and scripts it links to.
 * @param {string} baseUrl
 * @param {string} pagePath
 */
async function checkPage(baseUrl, pagePath) {
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

/**
 * Waits up to 30 seconds for the server to answer, or for it to exit.
 * @param {import('child_process').ChildProcess} server
 * @param {string} baseUrl
 */
async function waitForServer(server, baseUrl) {
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
