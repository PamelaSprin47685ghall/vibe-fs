/** agent-dsl-canary — data-driven. Scenario: scripts/agent-dsl.toml */
import { runCanary } from '../canary-driver.mjs';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../index.js';

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('agent-dsl canary static gate failed');
}
process.exit(await runCanary('agent-dsl'));
