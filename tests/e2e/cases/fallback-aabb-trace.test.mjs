/**
 * fallback-aabb-trace — the repository's central No-Go evidence.
 *
 * Scenario: scenarios/fallback-aabb-trace.toml
 *
 * FALLBACK-002's Offset cycles `(n+1) mod 4`, so one Logical Run's provider-visible model
 * trajectory is A, A, B, B — and there is no fifth automatic attempt. The scenario declares
 * the trajectory with `assertModelTrajectory`; this file adds the one thing a scenario
 * cannot express, which is writing that trajectory out as a release artifact.
 *
 * ── what K9 removed from this file ──────────────────────────────────────────
 *
 * 170 lines of hand-rolled flow: its own `setupScenario`, its own `loadScripts`, its own
 * `wait`/`waitFact`/`awaitEvent` interpreter, and its own session binding. Every one was a
 * second implementation of a driver verb, and they had already drifted:
 *
 *   it filtered the trajectory by two hard-coded prompt substrings instead of by session, so
 *   any other session sending the same text would have been counted
 *
 *   it carried `rawModels.length === 5 → slice(1)` to tolerate a duplicated first attempt —
 *   assertion weakening of the kind VERIFY-002 forbids, in the one scenario whose entire
 *   purpose is an exact request count
 */

import assert from 'node:assert/strict';
import fs from 'node:fs';
import { execFileSync } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { runStaticGate } from '../support/index.js';
import { runCanary } from '../support/scenario-driver.mjs';
import { journalEventLines, factPayloads } from '../support/journal-observer.js';

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('fallback-aabb-trace scenario static gate failed');
}

/** Read the real journal envelopes carrying a fact name. */
function factsIn(workDir, name) {
  return journalEventLines(workDir)
    .map((line) => {
      try { return JSON.parse(line); } catch { return null; }
    })
    .filter((fact) => fact && JSON.stringify(fact).includes(name));
}

function countFact(workDir, name) {
  return factsIn(workDir, name).length;
}

function fieldValues(value, fieldName, values = []) {
  if (value === null || value === undefined) return values;
  if (Array.isArray(value)) {
    for (const item of value) fieldValues(item, fieldName, values);
    return values;
  }
  if (typeof value !== 'object') return values;

  for (const [key, child] of Object.entries(value)) {
    if (key.toLowerCase() === fieldName.toLowerCase()) {
      if (typeof child === 'string' || typeof child === 'number') values.push(String(child));
      else if (child && typeof child === 'object') {
        if (typeof child.Value === 'string') values.push(child.Value);
        if (typeof child.value === 'string') values.push(child.value);
        // A typed identity serialises as `["Tag","value"]`; the value is the
        // payload, not the Fable tag.
        if (Array.isArray(child) && child.length === 2 && typeof child[1] === 'string') {
          values.push(child[1]);
        }
      }
    }
    fieldValues(child, fieldName, values);
  }
  return values;
}

function messagesOf(response) {
  const data = response?.data?.data ?? response?.data;
  return Array.isArray(data) ? data : [];
}

function messageInfo(message) {
  return message?.info ?? message ?? {};
}

function completedTime(message) {
  const info = messageInfo(message);
  return info.time?.completed ?? message?.time?.completed ?? info.completed ?? message?.completed;
}

function errorName(message) {
  const info = messageInfo(message);
  const error = info.error ?? message?.error;
  return error?.name ?? error?.type ?? info.errorName ?? message?.errorName;
}

async function assertFailureEvidence(scenario, ctx) {
  const response = await scenario.client.messages(ctx.sessionId);
  assert.ok(response.ok, `failed to read AABB Host transcript: ${JSON.stringify(response.data)}`);

  const messages = messagesOf(response);
  const assistants = messages.filter((message) => messageInfo(message).role === 'assistant');
  const settledFailures = assistants.filter((message) => completedTime(message) !== undefined && errorName(message));
  assert.equal(settledFailures.length, 4, 'AABB must settle exactly four assistant failures');

  // Exactly the four advances of one modulo-4 cycle. A fifth would mean something
  // other than a settled provider failure spent the owner's cursor — the defect this
  // canary now pins: Host abort cleanup interrupts the Companion's blog call, and
  // LOOP-006 forbids that cleanup from advancing the primary A/A/B/B cursor.
  const payloads = factPayloads(scenario.host.workDir, 'FallbackCursorAdvanced');
  assert.equal(
    payloads.length,
    4,
    `AABB must write exactly the four cursor advances of one modulo-4 cycle: ${JSON.stringify(payloads)}`,
  );
  const aabbPayloads = [...payloads].sort(
    (left, right) => Number(left?.ConsecutiveFailureCount) - Number(right?.ConsecutiveFailureCount),
  );
  assert.equal(new Set(fieldValues(aabbPayloads, 'LogicalRunId')).size, 1, 'all AABB failures stay in one Logical Run');
  assert.equal(new Set(fieldValues(aabbPayloads, 'ProviderRun')).size, 4, 'each failed ProviderRun is distinct before dedupe');
  assert.deepEqual(fieldValues(aabbPayloads, 'PreviousOffset'), ['0', '1', '2', '3'], 'AABB previous offsets');
  assert.deepEqual(fieldValues(aabbPayloads, 'NextOffset'), ['1', '2', '3', '0'], 'AABB modulo-4 successors');
  assert.deepEqual(fieldValues(aabbPayloads, 'ConsecutiveFailureCount'), ['1', '2', '3', '4'], 'AABB failures are consecutive');
}

/**
 * Write the proven trajectory where the release package expects it.
 *
 * Reads `ctx.modelTrajectory`, which `assertModelTrajectory` published after asserting it —
 * so the artifact cannot disagree with what was verified. Computing the list again here
 * would be a second source of truth for the one number this canary exists to establish.
 */
async function writeTraceEvidence(scenario, ctx) {
  const out = process.env.AABB_TRACE_OUT || '';
  if (out === '') return;

  const models = ctx.modelTrajectory;
  assert.ok(Array.isArray(models), 'writeTrace must run after assertModelTrajectory');

  fs.writeFileSync(
    out,
    [
      'provider-visible same-run AABB',
      `models=${JSON.stringify(models)}`,
      `fallbackFailures=${countFact(scenario.host.workDir, 'FallbackCursorAdvanced')}`,
      `requests=${models.length}`,
    ].join('\n') + '\n',
  );
}

process.exit(await runCanary('fallback-aabb-trace', {
  customs: {
    assertFailureEvidence,
    writeTrace: writeTraceEvidence,
  },
}));
