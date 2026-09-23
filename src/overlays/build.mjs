// @ts-check
/// <reference types="node" />
/**
 * Builds each overlay component in src/components/<name>/ into dist/<name>/.
 *
 * - <name>.ts and <name>.css are esbuild entry points: bundled (imports and CSS @imports are
 *   inlined) and, with --watch, rebuilt by esbuild on change. Don't import a component's .css
 *   from its .ts; esbuild would emit the CSS twice.
 * - Every other file (.html, images, fonts, …) is copied as-is. With --watch, a file watcher
 *   re-copies it on change and removes it from dist/ when deleted.
 *
 * Adding or removing a component folder requires restarting --watch.
 *
 * Run with: npm run build (or npm run build:watch)
 */
import fs from 'fs';
import path from 'path';
import * as esbuild from 'esbuild';

const watch = process.argv.includes('--watch');
const componentsDir = 'src/components';
const outDir = 'dist';

if (!fs.existsSync('src/generated')) {
  console.error("'src/generated' does not exist. Run 'dotnet build' first.");
  process.exit(1);
}

const components = fs
  .readdirSync(componentsDir, { withFileTypes: true })
  .filter((entry) => entry.isDirectory())
  .map((entry) => entry.name);

for (const name of components) {
  if (!fs.existsSync(path.join(componentsDir, name, `${name}.ts`))) {
    console.error(`Component '${name}' needs an entry file: ${componentsDir}/${name}/${name}.ts`);
    process.exit(1);
  }
}

fs.rmSync(outDir, { recursive: true, force: true });
for (const name of components) {
  copyAssets(name);
}

/** @type {import('esbuild').BuildOptions} */
const buildOptions = {
  entryPoints: components.flatMap((name) => {
    const source = path.join(componentsDir, name, name);
    const out = path.join(name, name); // → dist/<name>/<name>.js and .css
    const entries = [{ in: `${source}.ts`, out }];
    if (fs.existsSync(`${source}.css`)) {
      entries.push({ in: `${source}.css`, out });
    }
    return entries;
  }),
  bundle: true,
  outdir: outDir,
  format: 'esm',
  target: 'es2022',
  platform: 'browser',
  sourcemap: true, // readable stack traces in OBS's browser dev tools
  logLevel: 'info',
};

if (watch) {
  const ctx = await esbuild.context(buildOptions);
  await ctx.watch();
  watchAssets();
  console.log('Watching for changes... (restart after adding or removing a component)');
} else {
  // esbuild has already printed the errors (logLevel: 'info'); just fail the process.
  await esbuild.build(buildOptions).catch(() => process.exit(1));
}

/**
 * esbuild produces .ts and .css output itself; everything else is copied.
 * @param {string} file
 */
function isBuiltByEsbuild(file) {
  return file.endsWith('.ts') || file.endsWith('.css');
}

/**
 * Copies a component file or folder (relative to componentsDir) into the same place under
 * outDir, skipping files esbuild builds.
 * @param {string} relativePath
 */
function copyAssets(relativePath) {
  const destination = path.join(outDir, relativePath);
  fs.mkdirSync(path.dirname(destination), { recursive: true });
  fs.cpSync(path.join(componentsDir, relativePath), destination, {
    recursive: true,
    filter: (source) => fs.statSync(source).isDirectory() || !isBuiltByEsbuild(source),
  });
}

/** Mirrors non-esbuild asset changes (add, edit, delete) into outDir while watching. */
function watchAssets() {
  /** @type {Map<string, NodeJS.Timeout>} */
  const pending = new Map();

  fs.watch(componentsDir, { recursive: true }, (_event, filename) => {
    if (!filename || isBuiltByEsbuild(filename)) {
      return;
    }
    // Editors often fire several events per save (temp file, rename, write); act once.
    clearTimeout(pending.get(filename));
    pending.set(
      filename,
      setTimeout(() => {
        pending.delete(filename);
        syncAsset(filename);
      }, 50)
    );
  });
}

/** @param {string} filename path relative to componentsDir */
function syncAsset(filename) {
  try {
    if (fs.existsSync(path.join(componentsDir, filename))) {
      copyAssets(filename);
      console.log(`[assets] copied ${filename}`);
    } else {
      fs.rmSync(path.join(outDir, filename), { recursive: true, force: true });
      console.log(`[assets] removed ${filename}`);
    }
  } catch (error) {
    // A file can be briefly locked mid-save on Windows; the next event will retry.
    console.warn(`[assets] couldn't sync ${filename}: ${error}`);
  }
}
