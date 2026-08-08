/** manager-missing-final-report — data-driven. Scenario: scenarios/manager-missing-final-report.toml
 *
 * Regression: a manager's fork subagent that stops with an EMPTY terminal must NOT be
 * concluded as MISSING_FINAL_REPORT. Per FALLBACK-008 an empty / XML-only terminal earns
 * an interaction repair (RepairOnce / AbandonRoundProduct, never FailSlot); the subagent
 * auto-retries and continues, and only a later proven terminal claims the run
 * (P0-RECOVERY-JOIN-001). Before the fix, HostForkRunLifecycle.complete delivered a
 * proven MISSING_FINAL_REPORT failure and the manager saw its subagent fail.
 *
 * The coder's first reply is empty (`finish=stop`, no formal text). The empty terminal is
 * repaired (bare "#" missing-final-report poke), the coder then produces a valid report,
 * and the manager's join must see the subagent completed — never MISSING_FINAL_REPORT.
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

import { runCanary } from '../support/scenario-driver.mjs';
import { runStaticGate } from '../support/index.js';

function runtimeFacts(workDir, factName) {
  const common = execFileSync('git', ['-C', workDir, 'rev-parse', '--git-common-dir'], {
    encoding: 'utf8',
  }).trim();
  const runtimeDir = path.join(
    path.isAbsolute(common) ? common : path.resolve(workDir, common),
    'wanxiangshu-next',
    'runtimes',
  );
  if (!fs.existsSync(runtimeDir)) return [];

  return fs.readdirSync(runtimeDir)
    .filter((name) => name.endsWith('.ndjson'))
    .flatMap((name) => fs.readFileSync(path.join(runtimeDir, name), 'utf8').split('\n'))
    .filter((line) => line.trim() !== '')
    .map((line) => JSON.parse(line))
    .filter((fact) => JSON.stringify(fact).includes(factName));
}

async function runtimeDirOf(workDir) {
  const common = execFileSync('git', ['-C', workDir, 'rev-parse', '--git-common-dir'], {
    encoding: 'utf8',
  }).trim();
  return path.join(
    path.isAbsolute(common) ? common : path.resolve(workDir, common),
    'wanxiangshu-next',
    'runtimes',
  );
}

async function oracle(scenario, ctx) {
  const runtimeDir = await runtimeDirOf(scenario.host.workDir);

  // The coder subagent must have completed (HandleCompleted), never a MISSING_FINAL_REPORT
  // failure. Read the journal's HandleCompleted facts and assert the manager's join saw the
  // coder as completed, not failed.
  const facts = runtimeFacts(scenario.host.workDir, 'HandleCompleted');
  assert.ok(facts.length >= 1, `manager must have joined the completed coder: ${JSON.stringify(facts)}`);

  // The completed coder's join blob must NOT carry a MISSING_FINAL_REPORT failure code, and
  // the manager's own transcript must show the coder's valid report text (proof the empty
  // terminal was repaired and continued, not concluded).
  const allFactText = fs
    .readdirSync(runtimeDir)
    .filter((name) => name.endsWith('.ndjson'))
    .map((name) => fs.readFileSync(path.join(runtimeDir, name), 'utf8'))
    .join('\n');

  assert.doesNotMatch(allFactText, /MISSING_FINAL_REPORT/,
    `the coder subagent must never be concluded as MISSING_FINAL_REPORT; facts:\n${allFactText.slice(0, 2000)}`);

  const response = await scenario.client.messages(ctx.sessionId);
  assert.ok(response.ok, `failed to read Host transcript: ${JSON.stringify(response.data)}`);
  const transcript = JSON.stringify(response.data ?? response);
  assert.ok(
    transcript.includes('missing-final-report canary is reachable.'),
    `the repaired coder must have produced its report in the Host transcript: ${transcript.slice(0, 2000)}`,
  );
}

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('manager-missing-final-report canary static gate failed');
}
process.exit(await runCanary('manager-missing-final-report', { customs: { oracle } }));