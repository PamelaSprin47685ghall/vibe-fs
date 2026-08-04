/** manager-full-loop — data-driven. Scenario: scenarios/manager-full-loop.toml */
import { runCanary } from '../support/scenario-driver.mjs';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../support/index.js';

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('manager-full-loop canary static gate failed');
}
process.exit(await runCanary('manager-full-loop'));
