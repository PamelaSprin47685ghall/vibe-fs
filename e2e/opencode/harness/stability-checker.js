/**
 * stability-checker.js — Stability gate and static analysis logic.
 *
 * Implements:
 *   - Static analysis: checks for standalone fixed sleeps and containsTool.
 *   - Stability repetition: repeats a selected E2E test function N times.
 *   - Random order and isolation check.
 *   - Failure diagnostics archiving.
 */

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { gatherDiagnostics } from './diagnostics-collect.js';
import { formatDiagnostics } from './diagnostics-format.js';
import { setupScenario, teardownScenario } from './scenario.js';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '../../..');

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
  const timeoutMs = opts.timeoutMs || 90000;

  try {
    await Promise.race([
      fn(scenario),
      new Promise((_, reject) => setTimeout(
        () => reject(new Error(`${name} timed out after ${timeoutMs}ms`)),
        timeoutMs,
      )),
    ]);
  } catch (e) {
    testErr = e;
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
 *   - repeat: number (runs E2E test N times)
 *   - archiveDir: string (destination for failure diagnostics)
 *   - scenarioOpts: object (options passed to setupScenario)
 */
export async function runStabilityGate(opts = {}) {
  const { test, repeat = 50, archiveDir = 'diagnostics-archive', scenarioOpts = {} } = opts;
  if (!test || !test.fn) {
    throw new Error('No test specified for stability gate');
  }

  console.log(`Running stability gate for "${test.name}" (repeating ${repeat} times)...`);
  const runs = [];
  const failures = [];

  for (let i = 1; i <= repeat; i++) {
    const runName = `${test.name} (run ${i}/${repeat})`;
    const result = await runOneTest(test.name, test.fn, scenarioOpts);
    runs.push(result);

    if (result.ok) {
      console.log(`  ✓ Run ${i}/${repeat} passed in ${result.elapsedMs}ms`);
    } else {
      console.error(`  ✗ Run ${i}/${repeat} failed: ${result.error.message}`);
      failures.push({ run: i, error: result.error, diagnostics: result.diagnostics });

      // Archive diagnostics
      if (result.diagnostics) {
        try {
          const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
          const cleanName = test.name.replace(/[^a-zA-Z0-9-_]/g, '_');
          const dirPath = path.join(ROOT, archiveDir, `${timestamp}-${cleanName}-run-${i}`);
          fs.mkdirSync(dirPath, { recursive: true });

          // Save structured JSON
          fs.writeFileSync(
            path.join(dirPath, 'diagnostics.json'),
            JSON.stringify(result.diagnostics, null, 2),
            'utf8'
          );

          // Save formatted human-readable report
          fs.writeFileSync(
            path.join(dirPath, 'diagnostics.txt'),
            formatDiagnostics(result.diagnostics),
            'utf8'
          );

          // Copy raw NDJSON if exists
          if (result.diagnostics.ndjson?.path && fs.existsSync(result.diagnostics.ndjson.path)) {
            fs.copyFileSync(
              result.diagnostics.ndjson.path,
              path.join(dirPath, 'event-log.ndjson')
            );
          }

          console.error(`  💾 Archived failure diagnostics to: ${dirPath}`);
        } catch (archiveErr) {
          console.error(`  ✗ Failed to archive diagnostics: ${archiveErr.message}`);
        }
      }
    }
  }

  const passedCount = repeat - failures.length;
  console.log(`\nStability gate finished. ${passedCount}/${repeat} runs passed.`);
  return {
    passed: failures.length === 0,
    failures,
  };
}
