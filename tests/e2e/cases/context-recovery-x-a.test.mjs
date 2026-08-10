/**
 * x-a-probe-before-crash — C10 layer-4 evidence for docs/what/context.md: arming lost across the crash; no probe send; no promote.
 *
 * One scenario per case file: the e2e runner's bounded pool schedules case FILES, so the four
 * X-* scenarios behind one file serialized four Host lifetimes inside a single slot and became
 * the suite's critical path. The oracles are shared, not copied —
 * `../support/context-recovery-oracles.mjs`.
 */
import { fileURLToPath } from 'node:url';

import { runCanary } from '../support/scenario-driver.mjs';
import { runStaticGate } from '../support/index.js';
import { CUSTOMS } from '../support/context-recovery-oracles.mjs';

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('x-a-probe-before-crash scenario static gate failed');
}

process.exit(await runCanary('x-a-probe-before-crash', { customs: CUSTOMS['x-a-probe-before-crash'] }));
