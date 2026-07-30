/**
 * gate-testkit.mjs — Executable testkit quality gate runner.
 *
 * Proves environment isolation, strict FIFO, SSE reconnect/event waits,
 * and diagnostics/leak checks using extracted testkit APIs; no fixed sleeps.
 *
 * Run: node testkit/opencode/tests/gate-testkit.mjs
 */

import { cases } from './gate-cases.mjs';
import { budgetCases } from './gate-budget-cases.mjs';
import { coldBoundaryCases } from './gate-cold-boundary-cases.mjs';
import { deliveryCases } from './gate-delivery-cases.mjs';
import { pluginDependencyCase } from './gate-plugin-dependency-case.mjs';
import { scenarioRuntimeCases } from './gate-scenario-runtime-cases.mjs';
import { schemaCases } from './gate-schema-cases.mjs';
import { sourceCases } from './gate-source-cases.mjs';
import { pathCriterionCases } from './gate-path-criterion-cases.mjs';
import { projectionCases } from './gate-projection-cases.mjs';
import { runtimeKeyCases } from './gate-runtime-key-cases.mjs';
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

for (const c of [...cases, pluginDependencyCase, ...projectionCases, ...runtimeKeyCases, ...deliveryCases, ...coldBoundaryCases, ...schemaCases, ...scenarioRuntimeCases, ...sourceCases, ...pathCriterionCases, ...timeoutCases, ...budgetCases]) {
  await runCase(c);
}

console.log(`\n${passed} passed, ${failed} failed`);
process.exit(failed === 0 ? 0 : 1);
