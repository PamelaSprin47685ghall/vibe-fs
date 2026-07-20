/**
 * p0-canary-tests-recovery.js — Inert aggregate export for recovery E2E tests.
 */

import eventlogTests from './p0-canary-tests-recovery-eventlog.js';
import restartTests from './p0-canary-tests-recovery-restart.js';
import replayTests from './p0-canary-tests-recovery-replay.js';
import killTests from './p0-canary-tests-recovery-kill.js';

export default [
  ...eventlogTests,
  ...restartTests,
  ...replayTests,
  ...killTests,
];
