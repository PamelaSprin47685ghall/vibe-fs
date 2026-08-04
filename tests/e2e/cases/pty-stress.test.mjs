/** pty-stress — data-driven. Scenario: scenarios/pty-stress.toml */
import { runCanary } from '../support/scenario-driver.mjs';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../support/index.js';

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('pty-stress canary static gate failed');
}
process.exit(await runCanary('pty-stress'));
