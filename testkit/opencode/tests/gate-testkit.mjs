/**
 * gate-testkit.mjs — Executable testkit quality gate runner.
 *
 * Proves environment isolation, strict FIFO, SSE reconnect/event waits,
 * and diagnostics/leak checks using extracted testkit APIs; no fixed sleeps.
 *
 * Run: node testkit/opencode/tests/gate-testkit.mjs
 */

import { cases } from './gate-cases.mjs';
import { timeoutCases } from './gate-timeout-cases.mjs';

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

console.log('Running testkit/opencode gate tests...\n');

for (const c of [...cases, ...timeoutCases]) {
  await runCase(c);
}

console.log(`\n${passed} passed, ${failed} failed`);
process.exit(failed === 0 ? 0 : 1);
