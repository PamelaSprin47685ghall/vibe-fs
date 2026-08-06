/**
 * enforcer-repair-persist — ENFORCER-153 / DSL-003 canary.
 *
 * Scenario: scenarios/enforcer-repair-persist.toml
 * The Blogger child answers the first cycle with an EMPTY assistant text, the
 * AABB repair injects the synthetic `interaction-repair` message into the
 * transform output, and the Host persists it — the SECOND provider request
 * must carry the message. This is the real-Host evidence for the
 * transcript-accumulation simulation in enforcer-cycle-protocol.test.mjs.
 */
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../support/index.js';
import { runCanary } from '../support/scenario-driver.mjs';

const __filename = fileURLToPath(import.meta.url);

const REPAIR_MARKER = '# Protocol repair'

function isRepairMessage(message) {
  // The provider wire strips `info` (OpenCode serialises the internal info on
  // the persisted message, not on the chat.completions body). What survives to
  // the wire is the message body — the RepairInstruction text is the marker.
  // The info/source/requestKey contract itself is locked at the unit layer
  // (repairRequestKey, enforcer-cycle-protocol.test.mjs).
  const parts = message?.parts ?? []
  for (const part of parts) {
    if (part?.type === 'text' && typeof part?.text === 'string' && part.text.includes(REPAIR_MARKER)) return true
  }
  const content = message?.content
  if (typeof content === 'string' && content.includes(REPAIR_MARKER)) return true
  if (Array.isArray(content)) {
    return content.some((c) => c?.type === 'text' && typeof c?.text === 'string' && c.text.includes(REPAIR_MARKER))
  }
  return false
}

async function assertRepairPersisted(scenario) {
  const requests = scenario.provider?.requests ?? [];
  assert.ok(requests.length >= 2, `expected at least two blogger requests, got ${requests.length}`);

  // The injected message must appear in a request OTHER than the one whose
  // empty reply triggered the injection: Host persistence is the property
  // under test, not the transform output of the triggering request itself.
  const carries = [];
  for (let i = 1; i < requests.length; i++) {
    const messages = requests[i]?.messages ?? [];
    const hit = messages.find(isRepairMessage);
    if (hit) {
      carries.push({ request: i });
    }
  }
  assert.ok(
    carries.length >= 1,
    `no later provider request carries the injected repair message; requests=${requests.length}` +
      `\n${requests.map((r, i) => `${i}: ${(r.messages ?? []).length} messages, roles=${(r.messages ?? []).map((m) => m.role).join(',')}`).join('\n')}`,
  );
}

if (!runStaticGate([__filename]).passed) {
  throw new Error('enforcer-repair-persist canary static gate failed');
}

process.exit(
  await runCanary('enforcer-repair-persist', {
    customs: { assertRepairPersisted },
  }),
);
