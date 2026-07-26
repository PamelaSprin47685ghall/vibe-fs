/**
 * fallback-canary.mjs — Fallback durable failure recording and recovery.
 *
 * Proves:
 * 1. Real child provider failures record one durable failure per retry attempt.
 * 2. Child requests select the durable A/A/B/B fallback projection.
 * 3. After host restart, the linked child and fallback state are recovered.
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
import { WATCHDOG_TIMEOUT_MS } from '../watchdog-constants.js';
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
function expectFailure(scenario, phase, prompt, model, turn) {
  scenario.provider.expectError({
    id: `${phase}-failure`,
    lane: expectationLane('fallback', 'child', 'coder', turn, 'chat', 'parent'),
    status: 500,
    headers: { 'retry-after-ms': '0' },
    body: { error: { message: `mock provider failure ${phase}`, type: 'server_error' } },
    match: { containsText: [prompt], model },
  });
  scenario.provider.expectText({
    id: `${phase}-retry`,
    lane: expectationLane('fallback', 'child', 'coder', turn + 1, 'chat', 'parent'),
    text: `${phase} retry completed.`,
    match: { containsText: [prompt], model },
  });
}
async function awaitFailureSequence(scenario, phase, sessionId, afterSeq) {
  await scenario.provider.waitForExpectation(`${phase}-failure`, WATCHDOG_TIMEOUT_MS);
  await scenario.events.awaitEvent(
    (event) => event.seq > afterSeq
      && event.type === 'session.status'
      && event.sessionID === sessionId
      && event.properties?.status?.type === 'retry',
    WATCHDOG_TIMEOUT_MS,
  );
  scenario.watchdog?.advance({
    reason: 'fallback-provider-retry-recorded',
    lane: `session:${sessionId}`,
    blocking: true,
  });
  await scenario.provider.waitForExpectation(`${phase}-retry`, WATCHDOG_TIMEOUT_MS);
  await scenario.events.awaitEvent(
    (event) => event.seq > afterSeq && event.type === 'session.idle' && event.sessionID === sessionId,
    WATCHDOG_TIMEOUT_MS,
  );
  scenario.watchdog?.advance({
    reason: 'fallback-failed-session-idle',
    lane: `session:${sessionId}`,
    blocking: true,
  });
}
function expectParentRound(scenario, index, turn, parentPrompt, childPrompt, agent) {
  scenario.provider.expectToolCall({
    id: `parent-fork-${index}`,
    lane: expectationLane('fallback', 'parent', 'orchestrator', turn),
    tool: 'fork',
    args: { agent, prompt: childPrompt },
    match: {
      requiredTools: ['fork', 'join'],
      forbiddenTools: ['read', 'write', 'edit', 'bash', 'glob', 'grep', 'list', 'verdict'],
    },
  });
  scenario.provider.expectText({
    id: `parent-round-${index}`,
    lane: expectationLane('fallback', 'parent', 'orchestrator', turn + 1),
    text: `Parent completed fallback round ${index}.`,
    match: { containsText: [parentPrompt] },
  });
}
async function runChildRound(scenario, parentId, childId, index, parentPrompt) {
  const parentTurn = scenario.turn.start(parentId);
  const afterSeq = scenario.events.lastSeq;
  const response = await scenario.client.request('POST', `/session/${parentId}/prompt_async`, {
    body: {
      agent: 'orchestrator',
      parts: [{ type: 'text', text: parentPrompt }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(response.ok, `parent round ${index} failed: ${JSON.stringify(response.data)}`);
  await scenario.provider.waitForExpectation(`parent-fork-${index}`, WATCHDOG_TIMEOUT_MS);
  let actualChildId = childId;
  if (!actualChildId) {
    const childCreated = await scenario.events.awaitEvent(
      (event) => event.seq > afterSeq
        && event.type === 'session.created'
        && event.parentSessionID === parentId
        && event.sessionAgent === 'coder',
      WATCHDOG_TIMEOUT_MS,
    );
    actualChildId = childCreated.sessionID;
    assert.ok(actualChildId, 'fallback fork must create a real child session');
    scenario.sessionIds.push(actualChildId);
    bindLaneSession(scenario.provider, actualChildId, 'child');
    scenario.watchdog?.advance({
      reason: 'fallback-child-created',
      lane: `session:${actualChildId}`,
      blocking: true,
    });
    scenario.provider.expectText({
      id: 'child-blogger',
      lane: expectationLane('fallback', 'child-blogger-initial', 'blogger', 1, 'chat', 'child'),
      blocking: false,
      neverEnd: true,
      text: 'Fallback child background.',
      match: { containsText: ['You are the blogger of a coding agent session.', '"agent":"coder"'] },
    });
  }
  await parentTurn.awaitTerminal({
    timeoutMs: WATCHDOG_TIMEOUT_MS,
    requireActivity: true,
    requireAssistantTerminal: true,
    requireIdleAfterActivity: true,
  });
  await awaitFailureSequence(scenario, `round${index}`, actualChildId, afterSeq);
  const facts = countFallbackFacts(scenario.host.workDir);
  assert.equal(facts, index, `round ${index} must append exactly one durable failure fact, got ${facts}`);
  return { facts, childId: actualChildId };
}
let scenario;
try {
  assert.equal(runStaticGate([__filename]).passed, true, 'static gate');
  scenario = await setupScenario({
    project: { files: { 'AGENTS.md': 'fallback canary\n' } },
    strict: true,
    extraEnv: {
      WANXIANGSHU_MODEL_A: 'test/test-model',
      WANXIANGSHU_MODEL_B: 'test/test-model-b',
    },
  });

  scenario.provider.expectText({
    id: 'parent-blogger',
    lane: expectationLane('fallback', 'parent-blogger-initial', 'blogger', 1, 'chat', 'parent'),
    blocking: false,
    neverEnd: true,
    text: 'Fallback background.',
    match: { containsText: ['You are the blogger of a coding agent session.', '"agent":"orchestrator"'] },
  });
  scenario.provider.expectTitle({
    id: 'fallback-title',
    lane: expectationLane('fallback', 'fallback-title', 'title', 1, 'title'),
  });

  // Every child failure is retried by OpenCode once.  The first request in
  // each round is the durable fallback attempt; its retry stays on that
  // selected model and is asserted too.
  const childPrompts = [
    'Child fallback attempt 1.',
    'Child fallback attempt 2.',
    'Child fallback attempt 3.',
    'Child fallback attempt 4.',
  ];
  const parentPrompts = [
    'Trigger the first child fallback run.',
    'Trigger the second child fallback run.',
    'Trigger the third child fallback run.',
    'Trigger the fourth child fallback run.',
  ];
  const models = ['test-model', 'test-model', 'test-model-b', 'test-model-b'];

  childPrompts.forEach((prompt, index) =>
    expectFailure(scenario, `round${index + 1}`, prompt, models[index], index * 2 + 1));
  expectParentRound(scenario, 1, 1, parentPrompts[0], childPrompts[0], 'coder');

  const created = await scenario.client.createSession();
  const sessionId = getSessionId(created);
  assert.ok(sessionId, `session creation failed: ${JSON.stringify(created)}`);
  scenario.sessionIds.push(sessionId);
  bindLaneSession(scenario.provider, sessionId, 'fallback-title', 'parent');

  // A/A is observed before restart.  The projection must survive restart
  // before the B-side requests are sent.
  const firstRound = await runChildRound(
    scenario,
    sessionId,
    null,
    1,
    parentPrompts[0],
  );
  const childId = firstRound.childId;
  const factsAfterRound1 = firstRound.facts;
  const parentMessages = await scenario.client.messages(sessionId);
  const agentId = JSON.stringify(parentMessages.data).match(/agentId[^a-z0-9]+([a-z0-9]{6})/i)?.[1];
  assert.ok(agentId, `fork result did not expose reusable agent id: ${JSON.stringify(parentMessages.data)}`);
  for (let index = 1; index < childPrompts.length; index += 1) {
    expectParentRound(scenario, index + 1, index * 2 + 1, parentPrompts[index], childPrompts[index], agentId);
  }
  const secondRound = await runChildRound(
    scenario,
    sessionId,
    childId,
    2,
    parentPrompts[1],
  );
  const factsAfterRound2 = secondRound.facts;
  assert.ok(factsAfterRound2 > factsAfterRound1,
    `journal must accumulate before restart: round1=${factsAfterRound1}, round2=${factsAfterRound2}`);

  await scenario.restart();

  scenario.provider.expectText({
    id: 'parent-blogger-restart',
    lane: expectationLane('fallback', 'parent-blogger-restarted', 'blogger', 1, 'chat', 'parent'),
    blocking: false,
    neverEnd: true,
    text: 'Fallback restart background.',
    match: { containsText: ['You are the blogger of a coding agent session.', '"agent":"orchestrator"'] },
  });
  scenario.provider.expectText({
    id: 'child-blogger-restart',
    lane: expectationLane('fallback', 'child-blogger-restarted', 'blogger', 1, 'chat', 'child'),
    blocking: false,
    neverEnd: true,
    text: 'Fallback child restart background.',
    match: { containsText: ['You are the blogger of a coding agent session.', '"agent":"coder"'] },
  });

  const thirdRound = await runChildRound(
    scenario,
    sessionId,
    childId,
    3,
    parentPrompts[2],
  );
  const fourthRound = await runChildRound(
    scenario,
    sessionId,
    childId,
    4,
    parentPrompts[3],
  );
  const factsAfterRound3 = thirdRound.facts;
  const factsAfterRound4 = fourthRound.facts;
  assert.ok(factsAfterRound3 > factsAfterRound2,
    `restart must recover durable fallback state: round2=${factsAfterRound2}, round3=${factsAfterRound3}`);
  assert.ok(factsAfterRound4 > factsAfterRound3,
    `journal must accumulate after restart: round3=${factsAfterRound3}, round4=${factsAfterRound4}`);

  const observedModels = childPrompts.map((prompt, index) => {
    const requests = scenario.provider.requests.filter((request) =>
      request.__testkitHeaders?.['x-session-affinity'] === childId
      && request.messages?.at(-1)?.content === prompt);
    assert.ok(requests.length >= 2, `child round ${index + 1} must issue failure and retry requests`);
    assert.ok(requests.every((request) => request.model === models[index]),
      `child round ${index + 1} requests must use ${models[index]}, got ${requests.map((r) => r.model).join(',')}`);
    return requests[0].model;
  });
  assert.deepEqual(observedModels, models, 'child provider requests must follow durable A/A/B/B projection');

  scenario.provider.expectSatisfied();
  await teardownScenario(scenario);
  console.log(`Fallback canary passed: child provider A/A/B/B selected across restart; ${factsAfterRound4} durable failures recorded.`);
} catch (error) {
  console.error(`Fallback canary failed: ${error.stack || error}`);
  if (scenario?.provider?.unexpectedRequests) {
    console.error(`unexpected: ${JSON.stringify(scenario.provider.unexpectedRequests.slice(0, 2).map((r) => ({ reason: r.reason })))}`);
    console.error(`unexpected-details: ${JSON.stringify(scenario.provider.unexpectedRequests.slice(0, 4).map((r) => ({ model: r.body?.model, session: r.sessId, parent: r.parentSessionId, lastUser: r.body?.messages?.at(-1)?.content, candidates: r.candidates })))}`);
  }
  if (scenario?.provider?.requests) console.error(`child-models: ${JSON.stringify(scenario.provider.requests.filter((r) => JSON.stringify(r).includes('Child fallback attempt')).map((r) => ({ model: r.model, lastUser: r.messages?.at(-1)?.content })))}`);
  if (scenario?.host?.stderrLog) console.error(`host stderr: ${scenario.host.stderrLog.slice(-2000)}`);
  if (scenario) {
    try { await teardownScenario(scenario, { keepOnFailure: true }); } catch {}
  }
  process.exit(1);
}
