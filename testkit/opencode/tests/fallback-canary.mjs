/** fallback-canary — data-driven. Script: scripts/fallback.json
 *
 * Product semantics: Session does not own a permanent model. Host continues the
 * last user prompt's agent/model. After two durable failures, Side=B permanently;
 * subsequent user prompts that omit model receive Side B via chat.message.
 * Explicit user model always wins and starts a new Authority/Fallback epoch.
 */
import { runCanary } from '../canary-driver.mjs';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../index.js';

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('fallback canary static gate failed');
}
process.exit(await runCanary('fallback.json'));
