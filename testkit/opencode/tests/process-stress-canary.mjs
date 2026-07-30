/** process-stress-canary — data-driven. Scenario: scripts/process-stress.toml */
import { runCanary } from '../canary-driver.mjs';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../index.js';

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('process-stress canary static gate failed');
}
process.exit(await runCanary('process-stress'));
