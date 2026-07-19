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
import fallbackTests from './p0-canary-tests-fallback.js';
import fuzzyExecutorTests from './p0-canary-tests-fuzzy-executor.js';
import nudgeSubTests from './p0-canary-tests-nudge-sub.js';
import gateTests from './p0-canary-tests-gate.js';

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
  ...fallbackTests,
  ...gateTests,
]);

const exitCode2 = await runScenario({ ...common, contextLimit: 100000 }, nudgeSubTests);

const exitCode3 = await runScenario({
  ...common,
  contextLimit: 20000,
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
}, fuzzyExecutorTests);

process.exit(exitCode1 || exitCode2 || exitCode3);
