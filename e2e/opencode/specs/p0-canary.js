/**
 * p0-canary.js — P0 canary suite runner entry.
 *
 * Run: node e2e/opencode/specs/p0-canary.js
 *
 * Each test runs in its own scenario (one opencode process, one
 * workspace, one mock provider, one EventProbe). expectSatisfied() is
 * invoked automatically after each test by runScenario.
 *
 * PR1 compat: `allowSynthetic` + `allowTitleGen` keep the legacy canary
 * working. PR2 should remove these flags and migrate OC-NUDGE-001 to
 * use an explicit expectSyntheticTodoNudge() + expectNoMoreRequests().
 */

import { runScenario } from '../harness/scenario.js';
import basicTests from './p0-canary-tests-basics.js';
import ptyTests from './p0-canary-tests-pty.js';
import advancedTests from './p0-canary-tests-advanced.js';
import nudgeSubTests from './p0-canary-tests-nudge-sub.js';

const common = {
  plugin: true,
  timeoutMs: 90000,
  allowSynthetic: true,
  allowTitleGen: true,
};

const exitCode1 = await runScenario({ ...common, contextLimit: 20000 }, [
  ...basicTests,
  ...ptyTests,
  ...advancedTests,
]);

const exitCode2 = await runScenario({ ...common, contextLimit: 100000 }, nudgeSubTests);

process.exit(exitCode1 || exitCode2);
