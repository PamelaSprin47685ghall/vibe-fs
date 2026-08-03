/** manager-full-loop-canary — data-driven. Scenario: scripts/manager-full-loop.toml */
import { runCanary } from '../canary-driver.mjs';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../index.js';

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('manager-full-loop canary static gate failed');
}
process.exit(await runCanary('manager-full-loop'));
