/**
 * fallback-aabb-trace-canary — the repository's central No-Go evidence.
 *
 * Scenario: scripts/fallback-aabb-trace.toml
 *
 * FALLBACK-002's Offset cycles `(n+1) mod 4`, so one Logical Run's provider-visible model
 * trajectory is A, A, B, B — and there is no fifth automatic attempt. The scenario declares
 * the trajectory with `assertModelTrajectory`; this file adds the one thing a scenario
 * cannot express, which is writing that trajectory out as a release artifact.
 *
 * ── what K9 removed from this file ──────────────────────────────────────────
 *
 * 170 lines of hand-rolled flow: its own `setupScenario`, its own `loadScripts`, its own
 * `wait`/`waitFact`/`awaitEvent` interpreter, and its own session binding. Every one was a
 * second implementation of a driver verb, and they had already drifted:
 *
 *   it filtered the trajectory by two hard-coded prompt substrings instead of by session, so
 *   any other session sending the same text would have been counted
 *
 *   it carried `rawModels.length === 5 → slice(1)` to tolerate a duplicated first attempt —
 *   assertion weakening of the kind VERIFY-002 forbids, in the one canary whose entire
 *   purpose is an exact request count
 */

import assert from 'node:assert/strict';
import fs from 'node:fs';
import { execFileSync } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { runStaticGate } from '../index.js';
import { runCanary } from '../canary-driver.mjs';

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('fallback-aabb-trace canary static gate failed');
}

/** How many times a fact name appears across this runtime's journals. */
function countFact(workDir, name) {
  const common = execFileSync('git', ['-C', workDir, 'rev-parse', '--git-common-dir'], {
    encoding: 'utf8',
  }).trim();
  const runtimeDir = path.join(
    path.isAbsolute(common) ? common : path.resolve(workDir, common),
    'wanxiangshu-next',
    'runtimes',
  );
  if (!fs.existsSync(runtimeDir)) return 0;

  let total = 0;
  for (const file of fs.readdirSync(runtimeDir).filter((name) => name.endsWith('.ndjson'))) {
    for (const line of fs.readFileSync(path.join(runtimeDir, file), 'utf8').split('\n')) {
      if (line.includes(name)) total += 1;
    }
  }
  return total;
}

/**
 * Write the proven trajectory where the release package expects it.
 *
 * Reads `ctx.modelTrajectory`, which `assertModelTrajectory` published after asserting it —
 * so the artifact cannot disagree with what was verified. Computing the list again here
 * would be a second source of truth for the one number this canary exists to establish.
 */
async function writeTraceEvidence(scenario, ctx) {
  const out = process.env.AABB_TRACE_OUT || '';
  if (out === '') return;

  const models = ctx.modelTrajectory;
  assert.ok(Array.isArray(models), 'writeTrace must run after assertModelTrajectory');

  fs.writeFileSync(
    out,
    [
      'provider-visible same-run AABB',
      `models=${JSON.stringify(models)}`,
      `fallbackFailures=${countFact(scenario.host.workDir, 'FallbackCursorAdvanced')}`,
      `requests=${models.length}`,
    ].join('\n') + '\n',
  );
}

process.exit(await runCanary('fallback-aabb-trace', { customs: { writeTrace: writeTraceEvidence } }));
