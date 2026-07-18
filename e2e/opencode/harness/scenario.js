/**
 * scenario.js — Per-scenario isolation orchestration for OpenCode E2E.
 *
 * One scenario = one temp HOME + one temp XDG + one workspace
 *               + one StrictMockProvider + one opencode serve process.
 *
 * No shared-host mode. The runner auto-calls expectSatisfied() after
 * each test fn returns and aborts the test as failed if cleanup errors.
 * Cleanup errors are test errors, not warnings.
 *
 * Setup runs provider start, project-file write, git init, and host
 * start in parallel where dependencies allow (see scenario-parallel.js).
 */

import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { FsOracle, HttpClient, getSessionId } from './scenario-http.js';
import { runScenarioTests } from './scenario-runner.js';
import { setupScenarioParallel, Scenario } from './scenario-parallel.js';

function tmpDir() {
  return fs.mkdtempSync(path.join(os.tmpdir(), 'oc-e2e-'));
}

export { Scenario };

export async function setupScenario(opts = {}) {
  return setupScenarioParallel(opts, tmpDir);
}

export async function teardownScenario(scenario, { keepOnFailure = false } = {}) {
  if (scenario._tornDown) return;
  scenario._tornDown = true;
  const errors = [];
  try { await scenario.events.close(); } catch (e) { errors.push(`EventProbe: ${e.message}`); }
  for (const sid of scenario.sessionIds) {
    try { await scenario.client.abort(sid); } catch {}
  }
  try { await scenario.host.stop({ assert: true }); } catch (e) { errors.push(`Host: ${e.message}`); }
  try { await scenario.provider.stop(); } catch (e) { errors.push(`Provider: ${e.message}`); }
  if (!keepOnFailure) {
    try { fs.rmSync(scenario.scenarioDir, { recursive: true, force: true }); }
    catch (e) { errors.push(`Cleanup: ${e.message}`); }
  }
  if (errors.length > 0) {
    throw new Error(`E2E cleanup failed: ${errors.join('; ')}`);
  }
}

export async function runScenario(opts, tests) {
  return runScenarioTests(opts, tests, setupScenario);
}

export { FsOracle, HttpClient, getSessionId };
