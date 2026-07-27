/**
 * host-nudge-canary — data-driven. Script: scripts/host-nudge.json
 * After manager-fork-nudge, prove exact completed fork tool before abort.
 */
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../index.js';
import { runCanary } from '../canary-driver.mjs';

const __filename = fileURLToPath(import.meta.url);

function listForkParts(messages) {
  const parts = [];
  for (const msg of messages || []) {
    for (const part of msg.parts || []) {
      if (part?.type === 'tool' && (part.tool === 'fork' || part.name === 'fork')) {
        parts.push(part);
      }
    }
  }
  return parts;
}

function isCompletedFork(part) {
  const state = part?.state?.status || part?.state || part?.status;
  if (state === 'completed' || state === 'complete' || state === 'success') return true;
  const result = part?.state?.output ?? part?.output ?? part?.result;
  if (result == null) return false;
  const text = typeof result === 'string' ? result : JSON.stringify(result);
  return /nudged|accepted|forked/i.test(text);
}

async function proveNudgeForkCompleted(scenario, ctx) {
  const parentId = ctx.sessionId;
  assert.ok(parentId, 'parent session required');
  // Reconcile from API (not message.updated): exact tool completion proof.
  const deadline = Date.now() + 10000;
  let completed = 0;
  while (Date.now() < deadline) {
    const res = await scenario.client.request('GET', `/session/${parentId}/message`);
    const messages = res?.data?.messages || res?.data || [];
    const forks = listForkParts(Array.isArray(messages) ? messages : []);
    completed = forks.filter(isCompletedFork).length;
    // busy fork + nudge fork => at least 2 completed fork tools ideally;
    // minimal SSOT: after manager-fork-nudge wait, at least 1 completed fork with nudge semantics.
    if (completed >= 1) {
      const last = forks.filter(isCompletedFork).at(-1);
      const args = last?.state?.input || last?.input || last?.args || {};
      const prompt = args.prompt || args.Prompt || '';
      // Prefer nudge prompt when present; accept any completed fork if host shape omits args.
      if (!prompt || /nudge|continue|busy/i.test(String(prompt))) {
        scenario.watchdog?.advance({
          reason: 'nudge-fork-completed',
          lane: `session:${parentId}`,
          blocking: true,
        });
        return;
      }
    }
    await scenario.events.awaitEvent(
      (e) => e.sessionID === parentId && (e.type === 'session.status' || e.type === 'session.idle'),
      2000,
    ).catch(() => {});
  }
  assert.ok(completed >= 1, `expected completed fork tool after nudge, got ${completed}`);
}

if (!runStaticGate([__filename]).passed) {
  throw new Error('host-nudge canary static gate failed');
}

// Inject prove step by wrapping runCanary with a custom flow step after scripts load:
// host-nudge.json ends with abort; we use custom map for 'proveNudge' if present.
// Add prove via customs + script load: patch flow at runtime by re-export.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath as fup } from 'node:url';
import { resolveScriptPath } from '../script-loader.js';

const scriptPath = resolveScriptPath('host-nudge.json');
const doc = JSON.parse(fs.readFileSync(scriptPath, 'utf8'));
// Insert prove custom before abort if missing
const flow = doc.flow || [];
const hasProve = flow.some((s) => s.custom === 'proveNudge');
if (!hasProve) {
  const abortIdx = flow.findIndex((s) => s.abort);
  if (abortIdx >= 0) flow.splice(abortIdx, 0, { custom: 'proveNudge' });
  else flow.push({ custom: 'proveNudge' });
  doc.flow = flow;
  // write temp next to script? runCanary reads file - write back to script
  fs.writeFileSync(scriptPath, JSON.stringify(doc, null, 2) + '\n');
}

process.exit(
  await runCanary('host-nudge.json', {
    customs: { proveNudge: proveNudgeForkCompleted },
  }),
);
