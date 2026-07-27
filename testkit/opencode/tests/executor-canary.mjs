/** executor-canary — data-driven. Script: scripts/executor.json */
import { runCanary } from '../canary-driver.mjs';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../index.js';

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('executor canary static gate failed');
}
process.exit(await runCanary('executor.json'));
