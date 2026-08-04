/**
 * host-restart — data-driven. Scenario: scenarios/host-restart.toml
 * Post-restart nudge via flow's bindChild + prompt steps.
 */
import { runCanary } from '../support/scenario-driver.mjs';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../support/index.js';

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('host-restart scenario static gate failed');
}
process.exit(await runCanary('host-restart'));
