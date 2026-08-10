/**
 * stability-checker.js — Static analysis gate for E2E entry sources.
 *
 * Implements:
 *   - Static analysis: checks for standalone fixed sleeps.
 *
 * G4R-4 retired the multi-canary `runStabilityGate` repeat/shuffle path.
 * Long Stroke (`tests/e2e/entry.test.mjs`) is the sole top-level E2E entry and
 * uses `runStaticGate` only.
 */

import fs from 'node:fs';

/**
 * Checks files for fixed sleep violations.
 * Returns { passed: boolean, violations: Array<{ file, line, type, message }> }
 *
 * A `containsTool` check used to sit beside this one, gated on the path prefix
 * `e2e/opencode/specs/`. Package W3 measured it dead twice over: that directory has never
 * existed in this repository, so the branch was unreachable at all 19 call sites, AND
 * `containsTool` occurs nowhere in `tests/e2e/`, `scripts/`, or `tests/unit/` — `git log -S` puts
 * its last three appearances in the 0.4 tree. Realigning the path would have bought a reachable
 * check with nothing to ever do, which is the same defect one layer over.
 *
 * Its responsibility is already discharged by something strictly stronger. Package K7 retired
 * the whole `contains*` predicate family at LOAD time: `legacy-fields.js:20` refuses
 * `containsText` with 'declare the turn text as a prefix', and `tests/gate-source-cases.mjs`
 * asserts that refusal. A scenario carrying the vocabulary does not load, so there is no later
 * source scan left to perform.
 */
export function runStaticGate(filePaths = []) {
  const violations = [];

  for (const filePath of filePaths) {
    if (!fs.existsSync(filePath)) continue;
    const content = fs.readFileSync(filePath, 'utf8');
    const lines = content.split('\n');

    lines.forEach((line, idx) => {
      const lineNum = idx + 1;

      // Check fixed sleep
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
 * Retired after G4R-4. Repeat-for-confidence / shuffle multi-canary stability
 * is gone; Long Stroke is the sole E2E entry. Callers must not revive this API.
 */
export async function runStabilityGate() {
  throw new Error(
    'runStabilityGate retired after G4R-4 (Long Stroke sole entry; no repeat-for-confidence)',
  );
}
