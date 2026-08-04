/** orchestrator-publish -- data-driven. Scenario: scenarios/orchestrator-publish.toml */
import { runCanary } from '../support/scenario-driver.mjs';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../support/index.js';

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('orchestrator-publish canary static gate failed');
}
process.exit(await runCanary('orchestrator-publish'));
