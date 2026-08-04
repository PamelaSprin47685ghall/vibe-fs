/** executor — data-driven. Scenario: scenarios/executor.toml */
import { runCanary } from '../support/scenario-driver.mjs';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../support/index.js';

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('executor canary static gate failed');
}
process.exit(await runCanary('executor'));
