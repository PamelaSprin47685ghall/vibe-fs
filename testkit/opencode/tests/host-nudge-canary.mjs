/** host-nudge-canary — data-driven. Script: scripts/host-nudge.json */
import { runCanary } from '../canary-driver.mjs';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../index.js';

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('host-nudge canary static gate failed');
}
process.exit(await runCanary('host-nudge.json'));
