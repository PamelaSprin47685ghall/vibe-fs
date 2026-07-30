/** orchestrator-publish-canary -- data-driven. Scenario: scripts/orchestrator-publish.toml */
import { runCanary } from '../canary-driver.mjs';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../index.js';

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('orchestrator-publish canary static gate failed');
}
process.exit(await runCanary('orchestrator-publish'));
