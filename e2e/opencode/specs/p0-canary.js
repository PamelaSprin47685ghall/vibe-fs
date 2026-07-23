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

import { runScenario } from '../../../testkit/opencode/scenario.js';
import basicTests from './p0-canary-tests-basics.js';
import ptyTests from './p0-canary-tests-pty.js';
import advancedTests from './p0-canary-tests-advanced.js';
import fallbackTests from './p0-canary-tests-fallback.js';
import fuzzyExecutorTests from './p0-canary-tests-fuzzy-executor.js';
import nudgeSubTests from './p0-canary-tests-nudge-sub.js';
import gateTests from './p0-canary-tests-gate.js';
import webTests from './p0-canary-tests-web.js';
import lifecycleTests from './p0-canary-tests-lifecycle.js';

const common = {
  plugin: true,
  timeoutMs: 90000,
  allowSynthetic: true,
  allowTitleGen: true,
  reuseHost: true,
};

const webTestsNormal = webTests.filter(t => t.name !== 'OC-WEB-012 MCP process failure = child + resources cleaned');
const webTestsFail = webTests.filter(t => t.name === 'OC-WEB-012 MCP process failure = child + resources cleaned');

const lifecycleP0 = lifecycleTests.filter((t) =>
  /OC-LIFE-00[45]|OC-LIFE-010|OC-LIFE-015/.test(t.name)
);

const exitCode1 = await runScenario({
  ...common,
  contextLimit: 20000,
  reuseHost: true,
  project: {
    'page_file_1.txt': '1\n',
    'page_file_2.txt': '2\n',
    'page_file_3.txt': '3\n',
    'page_file_4.txt': '4\n',
    'exhaust_file_1.txt': '1\n',
    'exhaust_file_2.txt': '2\n',
    'exhaust_file_3.txt': '3\n',
    'cleanup_file_1.txt': '1\n',
    'cleanup_file_2.txt': '2\n',
    'cleanup_file_3.txt': '3\n',
    'multi_pat_file_a.txt': 'content_pat_x\n',
    'multi_pat_file_b.txt': 'content_pat_y\n',
    'work_cwd_marker.txt': 'cwd-correct\n',
  }
}, [
  ...basicTests,
  ...ptyTests,
  ...advancedTests,
  ...fallbackTests,
  ...gateTests,
  ...webTestsNormal,
  ...lifecycleP0,
  ...nudgeSubTests,
  ...fuzzyExecutorTests,
]);

const exitCode2 = await runScenario({
  ...common,
  contextLimit: 20000,
  extraEnv: {
    STEALTH_BROWSER_MCP_FAIL: 'true'
  }
}, webTestsFail);

process.exit(exitCode1 || exitCode2);
