/** orchestrator-restart-publish — crash-after-candidate + conflict resume.
 *
 * Gates both:
 *   - orchestrator-restart-publish           (after-candidate exactly-once)
 *   - orchestrator-restart-publish-conflict  (conflicted rebase resume → Published)
 *
 * Conflict was previously un-gated as "flaky under Host-owned Finality";
 * production must still reach Published — soft-skip is not a fix.
 */
import { runCanary } from '../support/scenario-driver.mjs';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../support/index.js';

const SCENARIOS = [
  'orchestrator-restart-publish',
  'orchestrator-restart-publish-conflict',
];

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('orchestrator-restart-publish scenario static gate failed');
}

let code = 0;
for (const name of SCENARIOS) {
  const exit = await runCanary(name);
  if (exit !== 0) {
    code = exit;
    break;
  }
}
process.exit(code);
