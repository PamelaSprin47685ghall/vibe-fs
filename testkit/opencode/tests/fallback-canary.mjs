/**
 * fallback-canary.mjs — Fallback durable failure recording and recovery.
 *
 * Proves:
 * 1. Provider failure (500) records FallbackFailureRecorded in NDJSON journal
 *    via FallbackDetect SSE message heuristic (empty assistant turn).
 * 2. After host restart, fallback state is recovered from journal (Boot fold).
 * 3. A second failure advances the fallback projection (cumulative, not reset).
 *
 * Run: node testkit/opencode/tests/fallback-canary.mjs
 */

import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  runStaticGate,
  setupScenario,
  teardownScenario,
  getSessionId,
} from '../index.js';

const __filename = fileURLToPath(import.meta.url);

function journalLines(workDir) {
  const runtimeDir = path.join(workDir, '.wanxiangshu-next', 'runtimes');
  if (!fs.existsSync(runtimeDir)) return [];
  const lines = [];
  for (const file of fs.readdirSync(runtimeDir)) {
    if (!file.endsWith('.ndjson')) continue;
    for (const line of fs.readFileSync(path.join(runtimeDir, file), 'utf8').split('\n')) {
      if (line.trim()) lines.push(JSON.parse(line));
    }
  }
  return lines;
}

function countFallbackFacts(workDir) {
  return journalLines(workDir).filter((entry) =>
    JSON.stringify(entry).includes('FallbackFailureRecorded')).length;
}

async function drainExpectations(scenario, timeoutMs) {
  const deadline = Date.now() + timeoutMs;
  while (scenario.provider.remainingExpectations > 0 && Date.now() < deadline) {
    await new Promise((r) => setTimeout(r, 200));
  }
  await new Promise((r) => setTimeout(r, 500));
}

let scenario;
try {
  assert.equal(runStaticGate([__filename]).passed, true, 'static gate');

  scenario = await setupScenario({
    project: { files: { 'AGENTS.md': 'fallback canary\n' } },
    strict: true,
    watchdogMs: 1000,
    extraEnv: {
      WANXIANGSHU_MODEL_A: 'test/test-model',
      WANXIANGSHU_MODEL_B: 'test/test-model-b',
    },
  });
  scenario.provider.allowSyntheticContinuations();
  scenario.provider.allowTitleGeneration();
  scenario.provider.allowBloggerRequests();
  scenario.provider.allowOutOfOrder();

  // Phase 1: provider failure → journal records FallbackFailureRecorded.
  for (let i = 0; i < 4; i++) {
    scenario.provider.expectError({
      id: `fail-round1-${i}`,
      status: 500,
      body: { error: { message: 'mock provider failure', type: 'server_error' } },
    });
  }

  const created = await scenario.client.createSession();
  const sessionId = getSessionId(created);
  assert.ok(sessionId, `session creation failed: ${JSON.stringify(created)}`);
  scenario.sessionIds.push(sessionId);

  const prompt1 = await scenario.client.request('POST', `/session/${sessionId}/prompt_async`, {
    body: {
      parts: [{ type: 'text', text: 'Hello, this will fail.' }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(prompt1.ok, `prompt failed: ${JSON.stringify(prompt1.data)}`);

  await drainExpectations(scenario, 15000);

  const factsAfterRound1 = countFallbackFacts(scenario.host.workDir);
  assert.ok(factsAfterRound1 >= 1,
    `journal must contain FallbackFailureRecorded after provider 500, got ${factsAfterRound1}`);

  // Phase 2: restart → fallback state recovered → second failure accumulates.
  await scenario.restart();

  for (let i = 0; i < 4; i++) {
    scenario.provider.expectError({
      id: `fail-round2-${i}`,
      status: 500,
      body: { error: { message: 'mock provider failure round 2', type: 'server_error' } },
    });
  }

  const prompt2 = await scenario.client.request('POST', `/session/${sessionId}/prompt_async`, {
    body: {
      parts: [{ type: 'text', text: 'Hello again, this will also fail.' }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(prompt2.ok, `prompt2 failed: ${JSON.stringify(prompt2.data)}`);

  await drainExpectations(scenario, 15000);

  const factsAfterRound2 = countFallbackFacts(scenario.host.workDir);
  assert.ok(factsAfterRound2 > factsAfterRound1,
    `journal must accumulate across restart: round1=${factsAfterRound1}, round2=${factsAfterRound2}`);

  await teardownScenario(scenario);
  console.log(`Fallback canary passed: ${factsAfterRound1} failure(s) recorded, restart recovered, ${factsAfterRound2} cumulative.`);
} catch (error) {
  console.error(`Fallback canary failed: ${error.stack || error}`);
  if (scenario?.provider?.unexpectedRequests) {
    console.error(`unexpected: ${JSON.stringify(scenario.provider.unexpectedRequests.slice(0, 2).map((r) => ({ reason: r.reason })))}`);
  }
  if (scenario?.host?.stderrLog) console.error(`host stderr: ${scenario.host.stderrLog.slice(-2000)}`);
  if (scenario) {
    try { await teardownScenario(scenario, { keepOnFailure: true }); } catch {}
  }
  process.exit(1);
}
