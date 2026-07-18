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

const tests = [
  ...basicTests,
  ...ptyTests,
  ...advancedTests,
  ...nudgeSubTests,
];

const exitCode = await runScenario({
  plugin: true,
  timeoutMs: 90000,
  contextLimit: 20000,
  allowSynthetic: true,
  allowTitleGen: true,
}, tests);
process.exit(exitCode);
