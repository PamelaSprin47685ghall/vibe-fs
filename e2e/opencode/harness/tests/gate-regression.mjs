/**
 * gate-regression.mjs — Executable harness behavior regression runner.
 *
 * Run: node e2e/opencode/harness/tests/gate-regression.mjs
 */

import { cases } from './gate-cases.mjs';

let passed = 0;
let failed = 0;

async function runCase({ name, fn }) {
  const start = Date.now();
  try {
    await fn();
    passed++;
    console.log(`  ✓ ${name} (${Date.now() - start}ms)`);
  } catch (err) {
    failed++;
    console.error(`  ✗ ${name}: ${err.message}`);
  }
}

for (const c of cases) {
  await runCase(c);
}

console.log(`\n${passed} passed, ${failed} failed`);
process.exit(failed === 0 ? 0 : 1);
