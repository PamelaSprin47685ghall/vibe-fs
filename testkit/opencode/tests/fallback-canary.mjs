/** fallback-canary — data-driven. Script: scripts/fallback.json
 *
 * Product semantics: Fallback belongs to a Logical Run. New Authority Root
 * resets Failures=0/Side=A. Omit-model inherits LastAuthority.BaseModel only,
 * never old Side B. Same-run A/A/B/B requires host retry control; this canary
 * proves durable failure facts + omit-model BaseModel inheritance.
 */
import { runCanary } from '../canary-driver.mjs';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../index.js';

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('fallback canary static gate failed');
}
process.exit(await runCanary('fallback.json'));
