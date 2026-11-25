#!/usr/bin/env node
// Per-package coverage report and floor check (#30).
//
// Reads every Cobertura file under a results directory, merges them per assembly, keeps only the
// cl2j.* library assemblies, writes a markdown table, and exits 1 when a package sits under its
// floor in floors.json (next to this file).
//
//   node .github/coverage/check-coverage.mjs <results-dir> [--summary <file>] [--floors <file>]
//   node .github/coverage/check-coverage.mjs <results-dir> --print     (measure only, no floors)
//
// A line counts as covered when ANY test project executed it: the files are merged by
// (assembly, source file, line), keeping the highest hit count. A library is often exercised by a
// suite other than its own (cl2j.Scripting by the cl2j.Database tests), so a per-test-project
// figure would misreport it.

import { readdirSync, readFileSync, statSync, appendFileSync, existsSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const argv = process.argv.slice(2);
const flag = name => argv.includes(name);
const value = name => (flag(name) ? argv[argv.indexOf(name) + 1] : undefined);
const resultsDir = argv[0];
const summaryFile = value('--summary');
const floorsFile = value('--floors') ?? join(dirname(fileURLToPath(import.meta.url)), 'floors.json');

if (!resultsDir || resultsDir.startsWith('--') || !existsSync(resultsDir)) {
  console.error(`Results directory not found: ${resultsDir ?? '(none given)'}`);
  process.exit(2);
}

function* walk(dir) {
  for (const name of readdirSync(dir)) {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) yield* walk(path);
    else if (name.endsWith('.cobertura.xml')) yield path;
  }
}

// Library assemblies only: cl2j.* minus the projects that test, sample or benchmark them. Third-party
// assemblies (Testcontainers, Docker.DotNet, ...) never match the cl2j. prefix.
const isLibrary = name =>
  name.startsWith('cl2j.') && !/\.(Tests|IntegrationTests|IntegrationTests\.Shared|Benchmarks)$/.test(name);

const files = [...walk(resultsDir)];
if (files.length === 0) {
  console.error(`No *.cobertura.xml under ${resultsDir}.`);
  process.exit(2);
}

// assembly -> Map("file|line" -> highest hit count)
const assemblies = new Map();
for (const file of files) {
  const xml = readFileSync(file, 'utf8');
  for (const pm of xml.matchAll(/<package\b[^>]*\bname="([^"]*)"[^>]*>([\s\S]*?)<\/package>/g)) {
    if (!isLibrary(pm[1])) continue;
    const lines = assemblies.get(pm[1]) ?? new Map();
    assemblies.set(pm[1], lines);
    for (const cm of pm[2].matchAll(/<class\b[^>]*\bfilename="([^"]*)"[^>]*>([\s\S]*?)<\/class>/g)) {
      for (const lm of cm[2].matchAll(/<line\b[^>]*\bnumber="(\d+)"[^>]*\bhits="(\d+)"/g)) {
        const key = `${cm[1]}|${lm[1]}`;
        lines.set(key, Math.max(lines.get(key) ?? 0, Number(lm[2])));
      }
    }
  }
}

const measured = new Map(
  [...assemblies.entries()].map(([name, lines]) => {
    const valid = lines.size;
    const covered = [...lines.values()].filter(hits => hits > 0).length;
    return [name, { valid, covered, pct: valid === 0 ? 0 : (covered / valid) * 100 }];
  }),
);

if (flag('--print')) {
  for (const [name, m] of [...measured].sort(([a], [b]) => a.localeCompare(b)))
    console.log(`${name}\t${m.covered}/${m.valid}\t${m.pct.toFixed(1)}`);
  process.exit(0);
}

const { floors } = JSON.parse(readFileSync(floorsFile, 'utf8'));
const failures = [];
const table = ['| Package | Lines | Covered | Coverage | Floor | |', '|---|---:|---:|---:|---:|---|'];

for (const name of [...new Set([...Object.keys(floors), ...measured.keys()])].sort()) {
  const m = measured.get(name);
  const floor = floors[name];
  if (floor === undefined) {
    failures.push(`${name} is measured (${m.pct.toFixed(1)}%) but has no entry in floors.json`);
    table.push(`| ${name} | ${m.valid} | ${m.covered} | ${m.pct.toFixed(1)}% | none | ❌ add a floor |`);
  } else if (m === undefined) {
    failures.push(`${name} has a floor of ${floor}% but no coverage was reported for it`);
    table.push(`| ${name} | 0 | 0 | n/a | ${floor}% | ❌ not reported |`);
  } else if (m.pct + 1e-9 < floor) {
    failures.push(`${name} is at ${m.pct.toFixed(1)}%, under its floor of ${floor}%`);
    table.push(`| ${name} | ${m.valid} | ${m.covered} | ${m.pct.toFixed(1)}% | ${floor}% | ❌ below floor |`);
  } else {
    table.push(`| ${name} | ${m.valid} | ${m.covered} | ${m.pct.toFixed(1)}% | ${floor}% | ✅ |`);
  }
}

const markdown = [
  '## Coverage by package',
  '',
  ...table,
  '',
  'Coverage says which lines ran, not whether the assertions are any good: code that another suite',
  'merely executes counts too.',
  '',
].join('\n');
console.log(markdown);
if (summaryFile) appendFileSync(summaryFile, markdown);

if (failures.length > 0) {
  console.error('Coverage check failed:');
  for (const f of failures) console.error(`  - ${f}`);
  console.error('A floor is raised when a change raises the number. It is never lowered to make a build pass.');
  process.exit(1);
}
