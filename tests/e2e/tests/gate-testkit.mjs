/**
 * gate-testkit.mjs — Executable testkit quality gate runner.
 *
 * Proves environment isolation, strict FIFO, SSE reconnect/event waits,
 * and diagnostics/leak checks using extracted testkit APIs; no fixed sleeps.
 *
 * Run: node tests/e2e/tests/gate-testkit.mjs
 */

import { cases } from './gate-cases.mjs';
import { arch010Cases } from './gate-arch010-cases.mjs';
import { budgetCases } from './gate-budget-cases.mjs';
import { coldBoundaryCases } from './gate-cold-boundary-cases.mjs';
import { degradationCases } from './gate-degradation-cases.mjs';
import { deliveryCases } from './gate-delivery-cases.mjs';
import { pluginDependencyCase } from './gate-plugin-dependency-case.mjs';
import { forestLibCases } from './gate-forest-lib-cases.mjs';
import { forestCases } from './gate-forest-cases.mjs';
import { mutationCases } from './gate-mutation-cases.mjs';
import { readinessCases } from './gate-readiness-cases.mjs';
import { unitRunnerCases } from './gate-unit-runner-cases.mjs';
import { scenarioRuntimeCases } from './gate-scenario-runtime-cases.mjs';
import { schemaCases } from './gate-schema-cases.mjs';
import { sourceCases } from './gate-source-cases.mjs';
import { pathCriterionCases } from './gate-path-criterion-cases.mjs';
import { singleSourceCases } from './gate-single-source-cases.mjs';
import { projectionCases } from './gate-projection-cases.mjs';
import { runtimeKeyCases } from './gate-runtime-key-cases.mjs';
import { timeoutCases } from './gate-timeout-cases.mjs';

// The worker pool admits at most GATE_CASE_CONCURRENCY cases. Per-spawn environment
// overrides, isolated temporary roots, listen(0), process groups, and ordered replay keep
// parallel execution from changing semantics.
//
// Eight is measured for this suite, whose cases are mostly pure/local and never fan out one
// OpenCode process per slot. It is intentionally independent from CANARY_MAX_PARALLEL.
// Concurrency counts are not durations and therefore do not belong in time-budget.js.
const GATE_CASE_CONCURRENCY = 8;

async function runCase({ name, fn }) {
  const start = Date.now();
  try {
    await fn();
    return { name, ok: true, ms: Date.now() - start };
  } catch (err) {
    return { name, ok: false, ms: Date.now() - start, err };
  }
}

const allCases = [...cases, pluginDependencyCase, ...projectionCases, ...runtimeKeyCases, ...deliveryCases, ...coldBoundaryCases, ...schemaCases, ...scenarioRuntimeCases, ...forestLibCases, ...sourceCases, ...pathCriterionCases, ...singleSourceCases, ...timeoutCases, ...budgetCases, ...readinessCases, ...unitRunnerCases, ...arch010Cases, ...forestCases, ...mutationCases, ...degradationCases];

console.log(`Running tests/e2e gate tests (${allCases.length} cases, ${GATE_CASE_CONCURRENCY} at a time)...\n`);

const results = new Array(allCases.length);
let next = 0;
const worker = async () => {
  while (next < allCases.length) {
    const index = next++;
    results[index] = await runCase(allCases[index]);
  }
};
await Promise.all(Array.from({ length: GATE_CASE_CONCURRENCY }, worker));

let passed = 0;
let failed = 0;
for (const r of results) {
  if (r.ok) {
    passed++;
    console.log(`  ✓ ${r.name} (${r.ms}ms)`);
  } else {
    failed++;
    console.error(`  ✗ ${r.name}: ${r.err.message}`);
  }
}

console.log(`\n${passed} passed, ${failed} failed`);
process.exit(failed === 0 ? 0 : 1);
