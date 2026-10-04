// @ts-check
/// <reference types="node" />
/**
 * Checks each C# source file's line coverage against its minimum in minimums.mjs (issue #18).
 *
 * Reads every coverage.cobertura.xml under the folder given (one per test project, written by
 * coverlet: `dotnet test --collect:"XPlat Code Coverage" --results-directory <folder>`), and
 * merges them line by line, since one file can be exercised by several test projects. Generated
 * code (anything under obj/) is left out.
 *
 * Fails when a file is below its minimum, when a file has no rule, or when a rule matches no file.
 *
 * Usage: node tools/coverage/check-coverage.mjs <results folder>
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { minimums } from './minimums.mjs';

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const resultsDir = process.argv[2];

if (!resultsDir || !fs.existsSync(resultsDir)) {
  console.error('Usage: node tools/coverage/check-coverage.mjs <results folder>');
  process.exit(2);
}

/** @type {Map<string, Map<number, boolean>>} repository-relative path -> line number -> covered */
const files = new Map();

const reports = findReports(resultsDir);
if (reports.length === 0) {
  console.error(`No coverage.cobertura.xml under ${resultsDir}. Run the tests with --collect:"XPlat Code Coverage".`);
  process.exit(2);
}

for (const report of reports) {
  readReport(report);
}

const failures = [];
const usedRules = new Set();
const rows = [];

for (const [file, lines] of [...files].sort(([a], [b]) => a.localeCompare(b))) {
  const covered = [...lines.values()].filter(Boolean).length;
  const percent = lines.size === 0 ? 100 : (100 * covered) / lines.size;
  const rule = minimums.find((candidate) => matches(candidate.match, file));

  if (!rule) {
    failures.push(`${file}: no minimum. Add one to tools/coverage/minimums.mjs, with the reason.`);
    continue;
  }

  usedRules.add(rule);
  rows.push({ file, percent, min: rule.min });
  if (percent < rule.min) {
    failures.push(`${file}: ${percent.toFixed(1)}% of lines covered, below its minimum of ${rule.min}% (${rule.why})`);
  }
}

for (const rule of minimums) {
  if (!usedRules.has(rule)) {
    failures.push(`Rule for ${rule.match} matches no source file: remove it from tools/coverage/minimums.mjs.`);
  }
}

const atMinimum = rows.filter((row) => row.min > 0 && row.percent - row.min < 5).length;
console.log(`Coverage: ${rows.length} files from ${reports.length} reports; ${atMinimum} within 5 points of their minimum.`);

if (failures.length > 0) {
  console.error(`\n${failures.length} coverage problem(s):`);
  for (const failure of failures) {
    console.error(`  ${failure}`);
  }
  process.exit(1);
}

console.log('Every file meets its minimum.');

/** @param {string} dir */
function findReports(dir) {
  /** @type {string[]} */
  const found = [];
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      found.push(...findReports(full));
    } else if (entry.name === 'coverage.cobertura.xml') {
      found.push(full);
    }
  }
  return found;
}

/**
 * Adds a report's lines to `files`. A line counts as covered if any report ran it.
 * @param {string} report
 */
function readReport(report) {
  const xml = fs.readFileSync(report, 'utf8');
  // Class file names are relative to the report's <source> folders (usually the repository's src/).
  const sources = [...xml.matchAll(/<source>([^<]+)<\/source>/g)].map((source) => source[1]);

  for (const match of xml.matchAll(/<class [^>]*filename="([^"]+)"[^>]*>([\s\S]*?)<\/class>/g)) {
    const file = resolveSource(match[1], sources);
    if (file === null || file.split('/').includes('obj')) {
      continue; // Outside the repository, or generated
    }

    // The class's own <lines>, after its <methods> (which repeat the same lines).
    const classLines = match[2].match(/<\/methods>\s*<lines>([\s\S]*?)<\/lines>/)?.[1] ?? match[2];
    const lines = files.get(file) ?? new Map();
    files.set(file, lines);

    for (const line of classLines.matchAll(/<line number="(\d+)" hits="(\d+)"/g)) {
      const number = Number(line[1]);
      lines.set(number, (lines.get(number) ?? false) || Number(line[2]) > 0);
    }
  }
}

/**
 * The repository-relative path of a report's file name, with forward slashes, or null if it isn't
 * in the repository.
 * @param {string} name
 * @param {string[]} sources
 */
function resolveSource(name, sources) {
  for (const source of sources.length > 0 ? sources : ['']) {
    const absolute = path.resolve(source, name);
    if (fs.existsSync(absolute)) {
      const relative = path.relative(repoRoot, absolute);
      return relative.startsWith('..') ? null : relative.replaceAll('\\', '/');
    }
  }
  return null;
}

/**
 * @param {string} pattern A file path, or a folder ending in "/".
 * @param {string} file
 */
function matches(pattern, file) {
  return pattern.endsWith('/') ? file.startsWith(pattern) : file === pattern;
}
