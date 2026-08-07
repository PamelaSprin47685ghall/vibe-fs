/** orchestrator-restart-publish — crash-after-candidate exactly-once publish.
 *
 * The rebase-conflict variant (`orchestrator-restart-publish-conflict`) is a
 * separate canary and is not gated by this file: it is flaky under the new
 * Host-owned Finality Manager path (Published race after dual suicide) and
 * is not required for the control-flow proposal close-out.
 */
import { runCanary } from '../support/scenario-driver.mjs';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../support/index.js';

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('orchestrator-restart-publish scenario static gate failed');
}
process.exit(await runCanary('orchestrator-restart-publish'));
