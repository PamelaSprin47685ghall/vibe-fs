/**
 * scenario-runner.js — Internal per-scenario loop helper.
 * Kept in its own module so scenario.js stays under the
 * 200-line Kolmogorov line budget and runWithTimeout stays
 * under the 50-line function budget.
 */

import { dumpDiagnostics } from './diagnostics.js';
import { teardownScenario } from './scenario.js';

export async function runScenarioTests(opts, tests, setupScenario) {
  let passed = 0;
  let failed = 0;
  const failures = [];
  const defaultTimeoutMs = opts.timeoutMs || 120000;

  for (const { name, fn } of tests) {
    const result = await runOne(name, fn, setupScenario, opts, defaultTimeoutMs);
    if (result.ok) {
      passed++;
      console.log(`  ✓ ${name} (${result.elapsedMs}ms)`);
    } else {
      failed++;
      failures.push({ name, error: result.error });
      console.error(`  ✗ ${name}: ${result.error.message}`);
      try { await dumpDiagnostics(result.scenario, result.error); } catch {}
    }
  }

  console.log(`\n${passed} passed, ${failed} failed`);
  if (failed > 0) reportFailures(failures);
  return failed === 0 ? 0 : 1;
}

async function runOne(name, fn, setupScenario, opts, timeoutMs) {
  const startTime = Date.now();
  console.log(`\n  ▶ ${name}`);
  let scenario;
  try {
    scenario = await setupScenario(opts);
  } catch (e) {
    return { ok: false, error: new Error(`setup failed: ${e.message}`), elapsedMs: Date.now() - startTime };
  }
  let testErr = null;
  try {
    await raceWithTimeout(fn(scenario), timeoutMs, name);
  } catch (e) {
    testErr = e;
  }
  try {
    scenario.provider.expectSatisfied();
  } catch (e) {
    testErr = testErr || e;
  }
  try {
    await teardownScenario(scenario, { keepOnFailure: !!testErr });
  } catch (e) {
    const err = new Error(`cleanup-failed: ${e.message}`);
    if (!testErr) testErr = err;
  }
  return testErr
    ? { ok: false, error: testErr, scenario, elapsedMs: Date.now() - startTime }
    : { ok: true, elapsedMs: Date.now() - startTime };
}

function raceWithTimeout(promise, timeoutMs, name) {
  return Promise.race([
    promise,
    new Promise((_, reject) => setTimeout(
      () => reject(new Error(`${name} timed out after ${timeoutMs}ms`)),
      timeoutMs,
    )),
  ]);
}

function reportFailures(failures) {
  console.error('Failures:');
  for (const f of failures) console.error(`  - ${f.name}: ${f.error.message}`);
}
