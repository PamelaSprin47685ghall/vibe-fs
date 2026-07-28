/** manager-full-loop-canary — data-driven. Script: scripts/manager-full-loop.json */
import { runCanary } from '../canary-driver.mjs';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../index.js';

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('manager-full-loop canary static gate failed');
}
process.exit(await runCanary('manager-full-loop.json'));
