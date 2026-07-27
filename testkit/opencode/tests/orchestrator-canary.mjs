/** orchestrator-canary -- data-driven. Script: scripts/orchestrator.json */
import { runCanary } from '../canary-driver.mjs';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../index.js';

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('orchestrator canary static gate failed');
}
process.exit(await runCanary('orchestrator.json'));
