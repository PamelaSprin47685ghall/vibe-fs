/**
 * stability-gate.mjs — Stability gate execution entrypoint.
 *
 * Usage:
 *   node --import tsx e2e/opencode/scripts/stability-gate.mjs [options]
 *
 * Options:
 *   --static-only       Only run the static analysis gates.
 *   --repeat <N>        Repeat the chosen test N times (default: 50).
 *   --test <name>       Choose the test to repeat (default: OC-FILE-001 write creates file with exact bytes).
 *   --shuffle           Shuffle all tests and run them in randomized order.
 *   --check-isolation   Run all tests individually to check isolation/consistency.
 *   --archive-dir <dir> Archive directory for failure logs (default: diagnostics-archive).
 */

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { runStaticGate, runStabilityGate } from '../../../testkit/opencode/stability-checker.js';
import { runScenario } from '../../../testkit/opencode/scenario.js';

// Import all E2E specs
import basicTests from '../specs/p0-canary-tests-basics.js';
import ptyTests from '../specs/p0-canary-tests-pty.js';
import advancedTests from '../specs/p0-canary-tests-advanced.js';
import fallbackTests from '../specs/p0-canary-tests-fallback.js';
import nudgeSubTests from '../specs/p0-canary-tests-nudge-sub.js';

// Stability gate test spec itself
import gateTests from '../specs/p0-canary-tests-gate.js';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '../../..');

const allTests = [
  ...basicTests,
  ...ptyTests,
  ...advancedTests,
  ...fallbackTests,
  ...nudgeSubTests,
  ...gateTests,
];

// Parse command line arguments
const args = process.argv.slice(2);
let staticOnly = args.includes('--static-only');
let shuffle = args.includes('--shuffle');
let checkIsolation = args.includes('--check-isolation');

let repeatVal = 50;
const repeatIdx = args.indexOf('--repeat');
if (repeatIdx !== -1 && args[repeatIdx + 1]) {
  repeatVal = parseInt(args[repeatIdx + 1], 10);
}

let testName = 'OC-FILE-001 write creates file with exact bytes';
const testIdx = args.indexOf('--test');
if (testIdx !== -1 && args[testIdx + 1]) {
  testName = args[testIdx + 1];
}

let archiveDir = 'diagnostics-archive';
const archiveIdx = args.indexOf('--archive-dir');
if (archiveIdx !== -1 && args[archiveIdx + 1]) {
  archiveDir = args[archiveIdx + 1];
}

console.log('=== WANXIANGSHU STABILITY GATES ===\n');

// 1. Run Static Gate checks
const specDir = path.join(ROOT, 'e2e/opencode/specs');
const specFiles = fs
  .readdirSync(specDir)
  .filter((f) => f.startsWith('p0-canary-tests-') && (f.endsWith('.js') || f.endsWith('.mjs')))
  .map((f) => path.join(specDir, f));

console.log('Running static analysis checks on E2E specs...');
const staticResult = runStaticGate(specFiles);
if (staticResult.passed) {
  console.log('  ✓ Static analysis checks passed (no fixed sleeps or forbidden containsTool assertions).\n');
} else {
  console.error('  ✗ Static analysis checks failed:');
  for (const v of staticResult.violations) {
    console.error(`    [VIOLATION] ${v.file}:${v.line} (${v.type}): ${v.message}`);
  }
  console.log('');
  process.exit(1);
}

if (staticOnly) {
  console.log('Static analysis checks completed successfully. Skipping E2E runs.');
  process.exit(0);
}

const scenarioOpts = {
  plugin: true,
  timeoutMs: 90000,
  allowSynthetic: true,
  allowTitleGen: true,
  contextLimit: 20000,
};

// Helper: Shuffle array
function shuffleArray(array) {
  const arr = [...array];
  for (let i = arr.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [arr[i], arr[j]] = [arr[j], arr[i]];
  }
  return arr;
}

if (shuffle) {
  console.log(`Shuffling and running all ${allTests.length} tests in a randomized order...`);
  const shuffled = shuffleArray(allTests);
  console.log(`Shuffled order: ${shuffled.map((t) => t.name).join(', ')}`);
  const exitCode = await runScenario(scenarioOpts, shuffled);
  process.exit(exitCode);
}

if (checkIsolation) {
  console.log(`Checking isolation of all ${allTests.length} tests individually...`);
  let passed = 0;
  let failed = 0;
  for (const t of allTests) {
    console.log(`Running in isolation: ${t.name}`);
    const exitCode = await runScenario(scenarioOpts, [t]);
    if (exitCode === 0) {
      passed++;
    } else {
      failed++;
      console.error(`  ✗ Test ${t.name} failed in isolation`);
    }
  }
  console.log(`\nIsolation check: ${passed}/${allTests.length} passed.`);
  process.exit(failed === 0 ? 0 : 1);
}

// 2. Run repeat-e2e stability check
const targetTest = allTests.find((t) => t.name === testName || t.name.includes(testName));
if (!targetTest) {
  console.error(`Target test "${testName}" not found in E2E spec files.`);
  process.exit(1);
}

const stabilityResult = await runStabilityGate({
  test: targetTest,
  repeat: repeatVal,
  archiveDir,
  scenarioOpts,
});

if (stabilityResult.passed) {
  console.log(`\nStability gate PASSED (repeated ${repeatVal} times with 0 failures).`);
  process.exit(0);
} else {
  console.error(`\nStability gate FAILED with ${stabilityResult.failures.length} failures out of ${repeatVal} runs.`);
  process.exit(1);
}
