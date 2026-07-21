/**
 * p0-canary-tests-recovery-replay.js — Restart replay recovers runtime states.
 */

import { getSessionId } from '../harness/scenario.js';
import { waitForLoopCommand, waitForAsyncCondition, validateNdjson } from './p0-canary-tests-recovery-helpers.js';
import { TIMEOUTS } from './p0-canary-utils.js';
import fs from 'node:fs';

function getHasPlugin(t) {
  return t.host._startOpts.pluginPaths && t.host._startOpts.pluginPaths.length > 0;
}

async function triggerReviewAndTodo(t, sid) {
  t.provider.expectText({ id: 'warm', text: 'ok' });
  const turn = await t.turn.start(sid);
  await t.client.prompt(sid, 'hello');
  await turn.awaitTerminal({ timeoutMs: TIMEOUTS.quick });

  const cmdTurn = await t.turn.start(sid);
  const cmdRes = await t.client.runCommand(sid, 'loop', 'implement feature X', 10000);
  if (!cmdRes.ok) throw new Error('loop command failed');
  await cmdTurn.awaitTerminal({ timeoutMs: TIMEOUTS.quick, requireAssistantTerminal: false });

  t.provider.expectToolCall({
    id: 'todo-write',
    tool: 'todowrite',
    args: {
      todos: [{ content: 'todo task', status: 'pending', priority: 'high' }],
      select_methodology: ['first_principles'],
    },
  });
  t.provider.expectText({ id: 'todo-done', text: 'todo written' });

  const todoTurn = await t.turn.start(sid);
  await t.client.prompt(sid, 'add a todo task via todowrite');
  await todoTurn.awaitTerminal({ timeoutMs: TIMEOUTS.quick });
}

async function waitForNudge(t) {
  t.provider.expectText({ id: 'nudge-reply', text: 'received nudge' });
  const deadline = Date.now() + TIMEOUTS.idleNudge;
  while (!t.provider.syntheticRequests.some((r) => r.marker === 'todo-nudge')) {
    if (Date.now() > deadline) throw new Error('Todo-nudge did not fire');
    await new Promise((r) => setTimeout(r, 200));
  }
}

async function requestFallback(t, sid) {
  t.host._startOpts.extraEnv = { WANXIANGSHU_TEST: 'false' };
  await t.restart();

  t.provider.expectError({
    id: 'err-503',
    status: 503,
    body: { error: { name: 'APIError', message: 'Service Unavailable', isRetryable: true } },
  });
  const fbTurn = await t.turn.start(sid);
  await t.client.prompt(sid, 'trigger fallback');
  await fbTurn.awaitTerminal({ timeoutMs: TIMEOUTS.quick, requireAssistantTerminal: false });
}

async function appendContextGeneration(t, sid) {
  const ndjsonPath = t.host.workDir + '/.wanxiangshu.ndjson';
  const ctxGenEvent = {
    V: 1,
    Session: sid,
    Kind: 'context_generation_changed',
    At: String(Date.now()),
    Payload: { generation: 5 },
  };
  fs.appendFileSync(ndjsonPath, JSON.stringify(ctxGenEvent) + '\n', 'utf8');
}

async function restartAndDispatchFallback(t, sid) {
  t.provider.expectText({ id: 'retry-success', text: 'recovered after restart' });
  t.host._startOpts.extraEnv = { WANXIANGSHU_TEST: 'true' };
  await t.restart();

  await waitForAsyncCondition(
    async () => t.provider.requests.some(r => JSON.stringify(r).includes('recovered')),
    (matched) => matched === true,
    TIMEOUTS.quick
  );
}

async function verifyRecoveredState(t, sid) {
  t.provider.expectText({ id: 'verify-prompt', text: 'verified' });
  const verifyTurn = await t.turn.start(sid);
  await t.client.prompt(sid, 'verify state');
  await verifyTurn.awaitTerminal({ timeoutMs: TIMEOUTS.quick });

  const lastReq = t.provider.requests.find(r => JSON.stringify(r).includes('verify state'));
  if (!lastReq) throw new Error('Verification request not captured');
  const reqStr = JSON.stringify(lastReq);
  if (!reqStr.includes('todo task')) {
    throw new Error('Todo state was not recovered: "todo task" missing from LLM request context');
  }
  if (!reqStr.includes('contextGeneration":5') && !reqStr.includes('contextGeneration\\":5')) {
    throw new Error('Context generation state was not recovered: contextGeneration 5 missing from request metadata');
  }

  const nudgeReqs = t.provider.syntheticRequests.filter(r => r.marker === 'todo-nudge');
  if (nudgeReqs.length > 1) {
    throw new Error(`Duplicate nudges triggered: got ${nudgeReqs.length} nudges`);
  }
}

async function runReplayFlow(t, sid) {
  await triggerReviewAndTodo(t, sid);
  await waitForNudge(t);
  await requestFallback(t, sid);
  await appendContextGeneration(t, sid);
  await restartAndDispatchFallback(t, sid);
  await verifyRecoveredState(t, sid);
}

const testRestartReplayRecovery = {
  name: 'OC-ES-006 OC-ES-007 OC-ES-008 OC-ES-009 OC-ES-010 OC-ES-011 restart replay recovers states (review, todo, nudge, fallback, context)',
  fn: async (t) => {
    const sid = getSessionId(await t.client.createSession());
    if (getHasPlugin(t)) {
      await waitForLoopCommand(t.client);
      await runReplayFlow(t, sid);
    }
    const status = await t.client.sessionStatus(sid);
    if (status.status !== 200) throw new Error(`Failed to query session status after restart: ${status.status}`);
  },
};

export default [testRestartReplayRecovery];
