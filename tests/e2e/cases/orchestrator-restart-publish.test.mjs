/**
 * orchestrator-restart-publish — Orchestrator → Manager publish survives a Host restart.
 *
 * One scenario per case file, and deliberately so: the runner's bounded pool schedules CASE
 * FILES, so two scenarios behind one file are two Host lifetimes inside one slot. Measured at
 * this suite's critical path — the paired file ran 20-30s while the pool sat at 3.5 of 8 slots,
 * i.e. the wall clock was paying for serialization the machine had capacity to avoid.
 *
 * The conflict half lives in `orchestrator-restart-publish-conflict.test.mjs`. Conflict was
 * previously un-gated as "flaky under Host-owned Finality"; production must still reach
 * Published — soft-skip is not a fix.
 */
import { runCanary } from '../support/scenario-driver.mjs';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../support/index.js';

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('orchestrator-restart-publish scenario static gate failed');
}

process.exit(await runCanary('orchestrator-restart-publish'));
