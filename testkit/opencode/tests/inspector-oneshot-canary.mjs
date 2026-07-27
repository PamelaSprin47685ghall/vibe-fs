/** inspector-oneshot-canary — data-driven. Script: scripts/inspector-oneshot.json */
import { runCanary } from '../canary-driver.mjs';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../index.js';

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('inspector-oneshot canary static gate failed');
}
process.exit(await runCanary('inspector-oneshot.json'));
