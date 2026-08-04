/** agent-dsl — data-driven. Scenario: scenarios/agent-dsl.toml */
import { runCanary } from '../support/scenario-driver.mjs';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../support/index.js';

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('agent-dsl canary static gate failed');
}
process.exit(await runCanary('agent-dsl'));
