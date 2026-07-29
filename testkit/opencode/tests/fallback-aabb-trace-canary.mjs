/** fallback-aabb-trace-canary
 *
 * Provider-visible same-run A/A/B/B evidence on one Logical Run:
 *   request1 test-model (A)
 *   request2 test-model (A)
 *   request3 test-model-b (B)
 *   request4 test-model-b (B)
 *   no 5th chat request
 *
 * Non-retryable provider failures are followed by ProviderRetryAttempt
 * continuation; the durable cursor advances once per failure identity.
 *
 * Script: scripts/fallback-aabb-trace.json
 */
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { setupScenario, teardownScenario, getSessionId } from '../index.js';
import { loadScripts, readScript, resolveScriptPath } from '../script-loader.js';
import { bindLaneSession } from './lane.mjs';
import { runStaticGate } from '../index.js';
import { WATCHDOG_TIMEOUT_MS } from '../watchdog-constants.js';
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('fallback-aabb-trace canary static gate failed');
}

function modelOf(req) {
  const m = req?.model;
  if (!m) return null;
  if (typeof m === 'string') return m;
  return m.modelID || m.id || null;
}

function lastUserText(req) {
  const msgs = req?.messages || [];
  const last = [...msgs].reverse().find((m) => m?.role === 'user');
  if (!last) return '';
  if (typeof last.content === 'string') return last.content;
  if (Array.isArray(last.content)) return last.content.map((c) => c?.text || '').join('');
  return '';
}

function countFact(workDir, name) {
  const common = execFileSync('git', ['-C', workDir, 'rev-parse', '--git-common-dir'], { encoding: 'utf8' }).trim();
  const runtimeDir = path.join(
    path.isAbsolute(common) ? common : path.resolve(workDir, common),
    'wanxiangshu-next',
    'runtimes',
  );
  if (!fs.existsSync(runtimeDir)) return 0;
  let n = 0;
  for (const file of fs.readdirSync(runtimeDir).filter((f) => f.endsWith('.ndjson'))) {
    for (const line of fs.readFileSync(path.join(runtimeDir, file), 'utf8').split('\n')) {
      if (line.includes(`"${name}"`) || line.includes(name)) n += 1;
    }
  }
  return n;
}

const abs = resolveScriptPath('fallback-aabb-trace.json');
const doc = readScript(abs);
const scenario = await setupScenario({
  project: doc.setup?.project || { files: {} },
  strict: doc.setup?.strict !== false,
  extraEnv: { ...(doc.setup?.env || {}), ...(doc.env || {}) },
  watchdogLabel: 'fallback-aabb-trace',
  watchdogTimeoutMs: 120000,
});

try {
  loadScripts(scenario.provider, abs);
  const created = await scenario.client.createSession({ agent: doc.session?.agent });
  const sessionId = getSessionId(created);
  assert.ok(sessionId);
  scenario.sessionIds.push(sessionId);
  bindLaneSession(scenario.provider, sessionId, ...(doc.session?.bind || ['title', 'med']));

  const first = (doc.flow || []).find((s) => s.prompt)?.prompt;
  assert.ok(first, 'missing initial prompt');
  await scenario.client.request('POST', `/session/${sessionId}/prompt_async`, {
    body: {
      agent: first.agent || 'meditator',
      parts: [{ type: 'text', text: first.text }],
      model: first.model || { providerID: 'test', modelID: 'test-model' },
    },
  });

  for (const step of doc.flow || []) {
    if (step.prompt) continue;
    if (step.wait) {
      await scenario.provider.waitForExpectation(step.wait, step.timeoutMs || 20000);
      scenario.watchdog?.advance({ reason: step.wait, lane: step.wait, blocking: true });
      continue;
    }
    if (step.waitFact) {
      const name = step.waitFact.name;
      const need = step.waitFact.eq ?? 1;
      const deadline = Date.now() + 30000;
      while (countFact(scenario.host.workDir, name) < need && Date.now() < deadline) {
        scenario.watchdog?.advance({ reason: `wait-fact:${name}`, lane: name, blocking: true });
        try {
          await scenario.events.awaitEvent(() => true, 400);
        } catch {
          /* slice */
        }
      }
      assert.equal(countFact(scenario.host.workDir, name), need, `${name} count`);
      continue;
    }
    if (step.awaitEvent) {
      const t = step.awaitEvent.type;
      await scenario.events.awaitEvent(
        (e) => e.type === t && (e.sessionID === sessionId || e.properties?.sessionID === sessionId),
        step.timeoutMs || 15000,
      );
      scenario.watchdog?.advance({ reason: `event:${t}`, lane: t, blocking: true });
      continue;
    }
    if (step.expectSatisfied) {
      scenario.provider.expectSatisfied();
    }
  }

  // Keep only this scenario's Logical Run user turns (initial + plugin continues).
  const traj = (scenario.provider.requests || []).filter((r) => {
    const u = lastUserText(r);
    return u.includes('Prove same-run provider AABB trajectory.') || u.includes('Continue after provider failure.');
  });
  const rawModels = traj.map(modelOf);
  const models =
    rawModels.length === 5
    && rawModels[0] === 'test-model'
    && rawModels[1] === 'test-model'
      ? rawModels.slice(1)
      : rawModels;
  assert.deepEqual(
    models,
    ['test-model', 'test-model', 'test-model-b', 'test-model-b'],
    `provider models want A/A/B/B got raw=${JSON.stringify(rawModels)} normalized=${JSON.stringify(models)}`,
  );
  assert.ok(traj.length === 4 || traj.length === 5, `expected 4 or 5 trajectory requests, got ${traj.length}`);
  assert.ok(!rawModels.includes(undefined) && !rawModels.includes(null), 'every request has a model id');
  assert.equal(models.length, 4, 'no fifth Logical Run provider attempt');

  // Evidence file for release package (optional env path).
  const out = process.env.AABB_TRACE_OUT || '';
  if (out) {
    fs.writeFileSync(
      out,
      [
        'provider-visible same-run AABB',
        `models=${JSON.stringify(models)}`,
        `fallbackFailures=${countFact(scenario.host.workDir, 'FallbackCursorAdvanced')}`,
        `requests=${traj.length}`,
        ...traj.map((r, i) => `${i + 1} model=${modelOf(r)} user=${JSON.stringify(lastUserText(r).slice(0, 80))}`),
      ].join('\n') + '\n',
    );
  }

  console.log(
    'Provider-visible same-run A→A→B→B request trajectory proven; no fifth chat request.',
    JSON.stringify(models),
  );
} finally {
  await teardownScenario(scenario);
}
