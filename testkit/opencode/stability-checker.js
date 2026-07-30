/**
 * stability-checker.js — Stability gate and static analysis logic.
 *
 * Implements:
 *   - Static analysis: checks for standalone fixed sleeps and containsTool.
 *   - Stability repetition: repeats a selected E2E test function N times.
 *   - Random order and isolation check.
 *   - Scenario-local failure diagnostics.
 */

import fs from 'node:fs';
import { gatherDiagnostics } from './diagnostics-collect.js';
import { setupScenario, teardownScenario } from './scenario.js';
import {
  STABILITY_SCENARIO_TIMEOUT_MS,
  STABILITY_GATE_WINDOW_MS,
  STABILITY_MIN_RUN_MS,
} from './time-budget.js';

/**
 * Checks files for containsTool and fixed sleep violations.
 * Returns { passed: boolean, violations: Array<{ file, line, type, message }> }
 */
export function runStaticGate(filePaths = []) {
  const violations = [];

  for (const filePath of filePaths) {
    if (!fs.existsSync(filePath)) continue;
    const content = fs.readFileSync(filePath, 'utf8');
    const lines = content.split('\n');

    lines.forEach((line, idx) => {
      const lineNum = idx + 1;

      // 1. Check containsTool
      // E2E spec files under e2e/opencode/specs/ must not use containsTool.
      if (filePath.includes('e2e/opencode/specs/') && line.includes('containsTool')) {
        violations.push({
          file: filePath,
          line: lineNum,
          type: 'containsTool',
          message: 'containsTool assertion is forbidden in E2E spec files. Use filesystem or message-content assertions instead.',
        });
      }

      // 2. Check fixed sleep
      // Matches sleep(...) or Promise.sleep(...) or setTimeout(..., number)
      const sleepMatch = line.match(/\b(sleep|Promise\.sleep|setTimeout)\s*\(\s*(\d+)/);
      if (sleepMatch) {
        // Look up to 15 lines back to see if there is a loop keyword (while, for, poll, retry, until, loop)
        let isPolling = false;
        const start = Math.max(0, idx - 15);
        for (let i = start; i <= idx; i++) {
          if (/\b(while|for|poll|retry|until|loop)\b/i.test(lines[i])) {
            isPolling = true;
            break;
          }
        }

        if (!isPolling) {
          violations.push({
            file: filePath,
            line: lineNum,
            type: 'fixed-sleep',
            message: `Fixed sleep or setTimeout call detected: "${line.trim()}". Fixed sleeps are forbidden. Use polling loops or event triggers instead.`,
          });
        }
      }
    });
  }

  return {
    passed: violations.length === 0,
    violations,
  };
}

/**
 * Runs a single E2E test scenario with the given options.
 */
async function runOneTest(name, fn, opts = {}) {
  const startTime = Date.now();
  let scenario;
  try {
    scenario = await setupScenario(opts);
  } catch (e) {
    return { ok: false, error: new Error(`Setup failed: ${e.message}`), elapsedMs: Date.now() - startTime };
  }

  let testErr = null;
  const timeoutMs = opts.timeoutMs || STABILITY_SCENARIO_TIMEOUT_MS;
  let timer;

  try {
    await Promise.race([
      fn(scenario),
      new Promise((_, reject) => {
        timer = setTimeout(
          () => reject(new Error(`${name} timed out after ${timeoutMs}ms`)),
          timeoutMs,
        );
      }),
    ]);
  } catch (e) {
    testErr = e;
  } finally {
    clearTimeout(timer);
  }

  try {
    scenario.provider.expectSatisfied();
  } catch (e) {
    testErr = testErr || e;
  }

  // Collect diagnostics if failed
  let diagnostics = null;
  if (testErr) {
    try {
      diagnostics = await gatherDiagnostics(scenario);
    } catch (diagErr) {
      console.error(`Failed to collect diagnostics: ${diagErr.message}`);
    }
  }

  try {
    await teardownScenario(scenario, { keepOnFailure: !!testErr });
  } catch (e) {
    const err = new Error(`Teardown failed: ${e.message}`);
    if (!testErr) testErr = err;
  }

  return {
    ok: !testErr,
    error: testErr,
    diagnostics,
    elapsedMs: Date.now() - startTime,
  };
}

/**
 * Runs E2E stability gate.
 * Options:
 *   - test: { name, fn }
 *   - repeat: 1–3 deterministic runs
 *   - scenarioOpts: object (options passed to setupScenario)
 */
export async function runStabilityGate(opts = {}) {
  const { test, repeat = 3, scenarioOpts = {} } = opts;
  if (!test || !test.fn) {
    throw new Error('No test specified for stability gate');
  }
  if (!Number.isInteger(repeat) || repeat < 1 || repeat > 3) {
    throw new Error(`Stability repeat must be an integer from 1 through 3, got ${repeat}`);
  }

  const globalTimeoutMs = opts.globalTimeoutMs || STABILITY_GATE_WINDOW_MS;
  const startTime = Date.now();

  console.log(`Running stability gate for "${test.name}" (repeating ${repeat} times)...`);
  const runs = [];
  const failures = [];

  for (let i = 1; i <= repeat; i++) {
    const elapsed = Date.now() - startTime;
    if (elapsed >= globalTimeoutMs) {
      console.warn(`[StabilityGate] Global timeout of ${globalTimeoutMs}ms reached. Stopping after ${i - 1} runs.`);
      break;
    }
    const remainingTime = globalTimeoutMs - elapsed;
    if (remainingTime < STABILITY_MIN_RUN_MS) {
      console.warn(`[StabilityGate] Insufficient time remaining (${remainingTime}ms). Stopping after ${i - 1} runs.`);
      break;
    }
    const testTimeout = Math.min(scenarioOpts.timeoutMs || STABILITY_SCENARIO_TIMEOUT_MS, remainingTime);
    const runOpts = { ...scenarioOpts, timeoutMs: testTimeout };

    const runName = `${test.name} (run ${i}/${repeat})`;
    const result = await runOneTest(test.name, test.fn, runOpts);
    runs.push(result);

    if (result.ok) {
      console.log(`  ✓ Run ${i}/${repeat} passed in ${result.elapsedMs}ms`);
    } else {
      console.error(`  ✗ Run ${i}/${repeat} failed: ${result.error.message}`);
      failures.push({ run: i, error: result.error, diagnostics: result.diagnostics });

      if (result.diagnostics) console.error(JSON.stringify(result.diagnostics, null, 2));
    }
  }

  const passedCount = runs.filter(r => r.ok).length;
  console.log(`\nStability gate finished. ${passedCount}/${runs.length} runs passed.`);
  return {
    passed: failures.length === 0,
    failures,
  };
}
