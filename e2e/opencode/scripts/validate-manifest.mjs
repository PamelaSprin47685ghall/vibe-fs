import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '../../..');
const BASELINE_FILE = path.join(ROOT, 'e2e/opencode/manifests/coverage-baseline.json');

// Load the TypeScript manifest with tsx. The runner invokes this script via
// `node --import tsx` so the import works in plain Node.
const { COVERAGE } = await import(
  new URL('../manifests/behavior-coverage.ts', import.meta.url).pathname
);

const LEVEL_ORDER = ['real-e2e', 'integration', 'unit', 'not-covered'];

function levelRank(level) {
  const idx = LEVEL_ORDER.indexOf(level);
  return idx === -1 ? LEVEL_ORDER.length : idx;
}

const errors = [];
const warnings = [];

function error(msg) {
  errors.push(msg);
  console.error(`  ✗ ${msg}`);
}

function warn(msg) {
  warnings.push(msg);
  console.warn(`  ⚠ ${msg}`);
}

function ok(msg) {
  console.log(`  ✓ ${msg}`);
}

function* entries() {
  for (const [area, arr] of Object.entries(COVERAGE)) {
    for (const e of arr) {
      yield { area, ...e };
    }
  }
}

const entriesList = [...entries()];

// 1. Unique IDs
const seen = new Map();
for (const e of entriesList) {
  if (seen.has(e.id)) {
    error(`duplicate id ${e.id} in ${e.area} and ${seen.get(e.id).area}`);
  } else {
    seen.set(e.id, e);
  }
}
ok(`${seen.size} unique coverage IDs`);

// 2. real-e2e validation
const realE2e = entriesList.filter((e) => e.level === 'real-e2e');
for (const e of realE2e) {
  const ctx = `${e.id} (${e.area})`;

  if (!e.spec) {
    error(`${ctx}: real-e2e entry is missing spec`);
    continue;
  }
  if (!e.test) {
    error(`${ctx}: real-e2e entry is missing test`);
    continue;
  }

  const specPath = path.resolve(ROOT, e.spec);
  if (!fs.existsSync(specPath)) {
    error(`${ctx}: spec file does not exist: ${e.spec}`);
    continue;
  }

  let tests;
  try {
    const mod = await import(specPath);
    tests = mod.default;
    if (!Array.isArray(tests)) {
      error(`${ctx}: spec module must export a default array of tests`);
      continue;
    }
  } catch (err) {
    error(`${ctx}: failed to import spec ${e.spec}: ${err.message}`);
    continue;
  }

  const found = tests.find((t) => t?.name === e.test);
  if (!found) {
    error(`${ctx}: test "${e.test}" not found in ${e.spec}`);
    continue;
  }

  if (!e.test.includes(e.id)) {
    error(`${ctx}: test name "${e.test}" must contain id ${e.id}`);
  }

  const content = fs.readFileSync(specPath, 'utf8');

  // 3. No direct plugin imports in real-e2e specs
  const pluginImportPattern =
    /from\s+['"](?:.*\/src\/Hosts\/OpenCode\/Plugin|.*\/build\/src\/Hosts\/OpenCode\/Plugin|@opencode-ai\/plugin).*?['"]|require\s*\(\s*['"](?:.*\/src\/Hosts\/OpenCode\/Plugin|.*\/build\/src\/Hosts\/OpenCode\/Plugin|@opencode-ai\/plugin).*?['"]\s*\)/;
  if (pluginImportPattern.test(content)) {
    error(`${ctx}: spec ${e.spec} imports plugin source directly`);
  }

  // 4. Evidence of real ProcessHost / scenario harness
  const realHostEvidence =
    /(ProcessHost|setupScenario|runScenario|createSession|opencode serve)/.test(
      content,
    );
  if (!realHostEvidence) {
    error(`${ctx}: spec ${e.spec} shows no evidence of real opencode ProcessHost`);
  }

  ok(`${ctx} -> ${e.spec}#${e.test}`);
}

// 5. Baseline enforcement
const notCovered = entriesList.filter((e) => e.level === 'not-covered');
const currentBaseline = {
  notCoveredCount: notCovered.length,
  entries: Object.fromEntries(
    entriesList.map((e) => [e.id, { level: e.level, note: e.note || undefined }]),
  ),
};

let baseline;
try {
  baseline = JSON.parse(fs.readFileSync(BASELINE_FILE, 'utf8'));
} catch {
  baseline = null;
}

if (!baseline || typeof baseline.entries !== 'object') {
  if (process.env.CI) {
    error(
      `coverage-baseline.json missing; run the validator locally to generate a committed baseline`,
    );
  } else {
    fs.writeFileSync(
      BASELINE_FILE,
      JSON.stringify(currentBaseline, null, 2) + '\n',
      'utf8',
    );
    ok(`wrote coverage baseline: ${notCovered.length} not-covered entries`);
  }
} else {
  if (notCovered.length > baseline.notCoveredCount) {
    error(
      `not-covered count increased from ${baseline.notCoveredCount} to ${notCovered.length}; ` +
        `new coverage must not increase not-covered entries`,
    );
  } else {
    ok(
      `not-covered count ${notCovered.length} <= baseline ${baseline.notCoveredCount}`,
    );
  }

  for (const e of entriesList) {
    const base = baseline.entries[e.id];
    if (!base) {
      // New IDs are allowed, but they must not be not-covered if baseline count is at limit.
      continue;
    }
    if (e.level === base.level) continue;
    const downgraded = levelRank(e.level) > levelRank(base.level);
    if (downgraded) {
      if (!e.note) {
        error(
          `${e.id}: level downgraded from ${base.level} to ${e.level} without a note`,
        );
      } else {
        warn(
          `${e.id}: level downgraded from ${base.level} to ${e.level} (note: ${e.note})`,
        );
      }
    } else {
      ok(`${e.id}: upgraded from ${base.level} to ${e.level}`);
    }
  }

  // 6. Missing IDs (removed from manifest)
  for (const id of Object.keys(baseline.entries)) {
    if (!seen.has(id)) {
      error(`${id}: id present in baseline but missing from manifest`);
    }
  }
}

// Summary
console.log('\n--- Coverage manifest validation ---');
console.log(`real-e2e entries: ${realE2e.length}`);
console.log(`not-covered entries: ${notCovered.length}`);
if (warnings.length) {
  console.log(`warnings: ${warnings.length}`);
}
if (errors.length) {
  console.error(`errors: ${errors.length}`);
  process.exit(1);
}
console.log('Manifest is valid.');
