/** pty-stress-canary — data-driven. Script: scripts/pty-stress.json */
import { runCanary } from '../canary-driver.mjs';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../index.js';

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('pty-stress canary static gate failed');
}
process.exit(await runCanary('pty-stress.json'));
