/** fallback-canary — data-driven. Script: scripts/fallback.json
 *
 * Product semantics: Fallback belongs to a Logical Run. New Authority Root
 * resets Failures=0/Side=A. Omit-model inherits LastAuthority.BaseModel only,
 * never old Side B.
 *
 * This canary proves durable FallbackFailureRecorded facts under host retry
 * signals + omit-model BaseModel inheritance.
 *
 * Final 0.4.0 still requires provider-visible same-run A→A→B→B request
 * trajectory evidence (resolveForSession unit path is covered; host re-prompt
 * after non-retryable provider error remains a HostContract gap).
 */
import { runCanary } from '../canary-driver.mjs';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../index.js';

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('fallback canary static gate failed');
}
process.exit(await runCanary('fallback.json'));
