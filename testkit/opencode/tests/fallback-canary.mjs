/** fallback-canary — data-driven. Script: scripts/fallback.json */
import { runCanary } from '../canary-driver.mjs';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../index.js';

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('fallback canary static gate failed');
}
process.exit(await runCanary('fallback.json'));
