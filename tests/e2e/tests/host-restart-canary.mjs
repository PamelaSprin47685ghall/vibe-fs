/**
 * host-restart-canary — data-driven. Scenario: scripts/host-restart.toml
 * Post-restart nudge via flow's bindChild + prompt steps.
 */
import { runCanary } from '../canary-driver.mjs';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../index.js';

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('host-restart canary static gate failed');
}
process.exit(await runCanary('host-restart'));
