/**
 * reviewer-restart-canary — data-driven. Script: scripts/reviewer-restart.json
 * Verdict tool surface intact across restart via flow createChild + restart.
 */
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../index.js';
import { runCanary } from '../canary-driver.mjs';

const __filename = fileURLToPath(import.meta.url);

async function oracleCheck(scenario, ctx, step) {
  const verdictReqs = scenario.provider.requests.filter(r =>
    r.tools?.some(t => (t.function?.name || t.name) === 'verdict')
  );
  assert.ok(verdictReqs.length >= 1, 'Reviewer must issue at least one verdict tool call');
  for (const req of verdictReqs) {
    const names = req.tools?.map(t => t.function?.name || t.name).filter(Boolean) || [];
    for (const f of ['write', 'edit', 'bash']) assert.ok(!names.includes(f), `Forbidden tool exposed: ${f}`);
  }
  const postReqs = scenario.provider.requests.filter(r =>
    JSON.stringify(r).includes('restart') && r.tools?.some(t => (t.function?.name || t.name) === 'verdict')
  );
  assert.ok(postReqs.length >= 1, 'Reviewer after restart must issue verdict tool calls');
}

if (!runStaticGate([__filename]).passed) process.exit(1);
process.exit(await runCanary('reviewer-restart.json', { customs: { oracle: oracleCheck } }));
