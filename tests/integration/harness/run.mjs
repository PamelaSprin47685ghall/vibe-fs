/**
 * harness/run.mjs — Executable harness quality runner.
 *
 * Proves environment isolation, strict FIFO, SSE reconnect/event waits,
 * and diagnostics/leak checks using extracted harness APIs; no fixed sleeps.
 *
 * Hang criterion: HARNESS_CASE_SILENCE_MS without a finished case (VERIFY-004).
 * Case completion is the only blocking advance — not case start, not console output.
 *
 * Run: node tests/integration/harness/run.mjs
 */

import { cases } from './cases.mjs';
import { arch010Cases } from './arch010-cases.mjs';
import { budgetCases } from './budget-cases.mjs';
import { coldBoundaryCases } from './cold-boundary-cases.mjs';
import { degradationCases } from './degradation-cases.mjs';
import { deliveryCases } from './delivery-cases.mjs';
import { pluginDependencyCase } from './plugin-dependency-case.mjs';
import { forestLibCases } from './forest-lib-cases.mjs';
import { forestCases } from './forest-cases.mjs';
import { mutationCases } from './mutation-cases.mjs';
import { readinessCases } from './readiness-cases.mjs';
import { unitRunnerCases } from './unit-runner-cases.mjs';
import { scenarioRuntimeCases } from './scenario-runtime-cases.mjs';
import { schemaCases } from './schema-cases.mjs';
import { sourceCases } from './source-cases.mjs';
import { pathCriterionCases } from './path-criterion-cases.mjs';
import { singleSourceCases } from './single-source-cases.mjs';
import { projectionCases } from './projection-cases.mjs';
import { runtimeKeyCases } from './runtime-key-cases.mjs';
import { timeoutCases } from './timeout-cases.mjs';
import { Watchdog } from '../../e2e/support/watchdog.js';
import { HARNESS_CASE_SILENCE_MS } from '../../e2e/support/time-budget.js';
import { bindHarnessFeed } from './progress.mjs';

// The worker pool admits at most GATE_CASE_CONCURRENCY cases. Per-spawn environment
// overrides, isolated temporary roots, listen(0), process groups, and ordered replay keep
// parallel execution from changing semantics.
//
// Eight is measured for this suite, whose cases are mostly pure/local and never fan out one
// OpenCode process per slot. It is intentionally independent from CANARY_MAX_PARALLEL.
// Concurrency counts are not durations and therefore do not belong in time-budget.js.
const GATE_CASE_CONCURRENCY = 8;

const allCases = [
  ...cases,
  pluginDependencyCase,
  ...projectionCases,
  ...runtimeKeyCases,
  ...deliveryCases,
  ...coldBoundaryCases,
  ...schemaCases,
  ...scenarioRuntimeCases,
  ...forestLibCases,
  ...sourceCases,
  ...pathCriterionCases,
  ...singleSourceCases,
  ...timeoutCases,
  ...budgetCases,
  ...readinessCases,
  ...unitRunnerCases,
  ...arch010Cases,
  ...forestCases,
  ...mutationCases,
  ...degradationCases,
];

console.log(
  `Running tests/integration harness tests (${allCases.length} cases, ${GATE_CASE_CONCURRENCY} at a time, ` +
    `${HARNESS_CASE_SILENCE_MS}ms case-silence window)...\n`,
);

const outstanding = new Set(allCases.map((c) => c.name));
let finished = 0;

const watchdog = new Watchdog({
  timeoutMs: HARNESS_CASE_SILENCE_MS,
  label: 'tests/integration/harness',
  onTimeout: () => {
    console.error(
      `harness: ${outstanding.size} case(s) still open: ${[...outstanding].slice(0, 20).join(', ')}` +
        (outstanding.size > 20 ? ` …(+${outstanding.size - 20})` : ''),
    );
    console.error(`harness: ${finished}/${allCases.length} finished before the silence`);
  },
});
bindHarnessFeed((progress) => watchdog.advance(progress));

async function runCase({ name, fn }) {
  const start = Date.now();
  try {
    await fn();
    return { name, ok: true, ms: Date.now() - start };
  } catch (err) {
    return { name, ok: false, ms: Date.now() - start, err };
  } finally {
    outstanding.delete(name);
    finished += 1;
    // Case completion is the causal advance — not start, not log lines.
    watchdog.advance({ reason: `case-complete:${name}`, lane: 'harness', blocking: true });
  }
}

const results = new Array(allCases.length);
let next = 0;
const worker = async () => {
  while (next < allCases.length) {
    const index = next++;
    results[index] = await runCase(allCases[index]);
  }
};

try {
  await Promise.all(Array.from({ length: GATE_CASE_CONCURRENCY }, worker));
} finally {
  watchdog.stop();
}

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
