/** inspector-oneshot — data-driven. Scenario: scenarios/inspector-oneshot.toml */
import { runCanary } from '../support/scenario-driver.mjs';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../support/index.js';

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('inspector-oneshot canary static gate failed');
}
process.exit(await runCanary('inspector-oneshot'));
