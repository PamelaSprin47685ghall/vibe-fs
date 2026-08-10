/** inspector-oneshot — data-driven SyncDelegate inspector canary.
 * Scenario: scenarios/inspector-oneshot.toml (reusable dedicated Session; not dispose-after).
 */
import { runCanary } from '../support/scenario-driver.mjs';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../support/index.js';

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('inspector-oneshot canary static gate failed');
}
process.exit(await runCanary('inspector-oneshot'));
