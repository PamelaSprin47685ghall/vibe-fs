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

/** The last user message's text on the wire, or null. */
function lastUserText(request) {
  const messages = request?.messages ?? [];
  for (let index = messages.length - 1; index >= 0; index -= 1) {
    const message = messages[index];
    if (message?.role !== 'user') continue;
    const content = message?.content;
    if (typeof content === 'string') return content;
    if (Array.isArray(content)) {
      const text = content
        .filter((chunk) => chunk?.type === 'text' && typeof chunk?.text === 'string')
        .map((chunk) => chunk.text)
        .join('');
      if (text !== '') return text;
    }
    for (const part of message?.parts ?? []) {
      if (part?.type === 'text' && typeof part?.text === 'string') return part.text;
    }
  }
  return null;
}

async function assertRepairPersisted(scenario) {
  const requests = scenario.provider?.requests ?? [];
  assert.ok(requests.length >= 2, `expected at least two blogger requests, got ${requests.length}`);

  // The SECOND blogger request is the automatic continuation after the AABB
  // repair: the injected RepairInstruction is its last user turn (scenario
  // turn `blogger-repair`). The marker must ride in THAT request's history —
  // Host persistence is the property under test, not the transform output of
  // the triggering request itself. A marker in any later request is not
  // evidence of persistence.
  const second = requests.find((request) => (lastUserText(request) ?? '').includes(REPAIR_MARKER));
  assert.ok(
    second,
    `no second blogger request carries the injected prompt; requests=${requests.length}\n` +
      requests.map((r, i) => `${i}: lastUser=${JSON.stringify(lastUserText(r))}, roles=${(r.messages ?? []).map((m) => m.role).join(',')}`).join('\n'),
  );
  assert.ok(
    (second.messages ?? []).some(isRepairMessage),
    `the SECOND blogger request must carry the injected repair message in its history; ` +
      `lastUser=${JSON.stringify(lastUserText(second))}\n` +
      JSON.stringify(second.messages, null, 1),
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
