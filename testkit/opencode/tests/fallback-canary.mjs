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
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import {
  runStaticGate,
  setupScenario,
  teardownScenario,
  getSessionId,
} from '../index.js';
import { bindLaneSession, expectationLane } from './lane.mjs';

const __filename = fileURLToPath(import.meta.url);

function journalLines(workDir) {
  const common = execFileSync('git', ['-C', workDir, 'rev-parse', '--git-common-dir'], { encoding: 'utf8' }).trim();
  const runtimeDir = path.join(path.isAbsolute(common) ? common : path.resolve(workDir, common), 'wanxiangshu-next', 'runtimes');
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

function expectFailure(scenario, phase, prompt, model) {
  scenario.provider.expectError({
    id: `${phase}-failure`,
    lane: expectationLane('fallback', phase, 'manager', 1),
    status: 500,
    headers: { 'retry-after-ms': '0' },
    body: { error: { message: `mock provider failure ${phase}`, type: 'server_error' } },
    match: { containsText: [prompt], model },
  });
  scenario.provider.expectText({
    id: `${phase}-retry`,
    lane: expectationLane('fallback', phase, 'manager', 2),
    text: `${phase} retry completed.`,
    match: { containsText: [prompt] },
  });
}

async function awaitFailureSequence(scenario, phase, sessionId, afterSeq) {
  await scenario.provider.waitForExpectation(`${phase}-failure`, 1000);
  await scenario.events.awaitEvent(
    (event) => event.seq > afterSeq
      && event.type === 'session.status'
      && event.sessionID === sessionId
      && event.properties?.status?.type === 'retry',
    1000,
  );
  scenario.watchdog?.advance({
    reason: 'fallback-provider-retry-recorded',
    lane: `session:${sessionId}`,
    blocking: true,
  });
  await scenario.provider.waitForExpectation(`${phase}-retry`, 1000);
  await scenario.events.awaitEvent(
    (event) => event.seq > afterSeq && event.type === 'session.idle' && event.sessionID === sessionId,
    1000,
  );
  scenario.watchdog?.advance({
    reason: 'fallback-failed-session-idle',
    lane: `session:${sessionId}`,
    blocking: true,
  });
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
  scenario.provider.expectText({
    id: 'manager-blogger',
    lane: expectationLane('fallback', 'manager-blogger-initial', 'blogger', 1, 'chat', 'round1'),
    blocking: false,
    text: 'Fallback background.',
    match: { containsText: ['You are the blogger of a coding agent session.', '"agent":"build"'] },
  });

  scenario.provider.expectTitle({
    id: 'fallback-title',
    lane: expectationLane('fallback', 'fallback-title', 'title', 1, 'title'),
  });

  // Phase 1: provider retry → journal records FallbackFailureRecorded.
  expectFailure(scenario, 'round1', 'Hello, this will fail.', 'test-model');

  const created = await scenario.client.createSession();
  const sessionId = getSessionId(created);
  assert.ok(sessionId, `session creation failed: ${JSON.stringify(created)}`);
  scenario.sessionIds.push(sessionId);
  bindLaneSession(scenario.provider, sessionId, 'fallback-title', 'round1', 'round2');

  const round1Seq = scenario.events.lastSeq;
  const prompt1 = await scenario.client.request('POST', `/session/${sessionId}/prompt_async`, {
    body: {
      parts: [{ type: 'text', text: 'Hello, this will fail.' }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(prompt1.ok, `prompt failed: ${JSON.stringify(prompt1.data)}`);

  await awaitFailureSequence(scenario, 'round1', sessionId, round1Seq);

  const factsAfterRound1 = countFallbackFacts(scenario.host.workDir);
  assert.ok(factsAfterRound1 >= 1,
    `journal must contain FallbackFailureRecorded after provider 500, got ${factsAfterRound1}`);

  // Phase 2: restart → fallback state recovered → second failure accumulates.
  await scenario.restart();

  scenario.provider.expectText({
    id: 'manager-blogger-restart',
    lane: expectationLane('fallback', 'manager-blogger-restarted', 'blogger', 1, 'chat', 'round1'),
    blocking: false,
    text: 'Fallback restart background.',
    match: { containsText: ['You are the blogger of a coding agent session.', '"agent":"build"'] },
  });

  expectFailure(scenario, 'round2', 'Hello again, this will also fail.', 'test-model');

  const round2Seq = scenario.events.lastSeq;
  const prompt2 = await scenario.client.request('POST', `/session/${sessionId}/prompt_async`, {
    body: {
      parts: [{ type: 'text', text: 'Hello again, this will also fail.' }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(prompt2.ok, `prompt2 failed: ${JSON.stringify(prompt2.data)}`);

  await awaitFailureSequence(scenario, 'round2', sessionId, round2Seq);

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
