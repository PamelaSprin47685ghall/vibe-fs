/**
 * p0-canary-tests-nudge-sub.js — Nudge / Subagent tests.
 * Kept under the 300-line Kolmogorov line budget.
 */

import { getSessionId } from '../harness/scenario.js';
import { extractToolNames, sleep, TIMEOUTS } from './p0-canary-utils.js';

function expectNoSessionError(t, sid) {
  t.events.expectCount({ type: 'session.error', sessionID: sid, count: 0 });
}

const tests = [

  // OC-NUDGE-001: completed human turn + idle fires exactly ONE nudge prompt;
  // a second idle after the nudge reply must NOT fire another nudge.
  // PR1 keeps the legacy `syntheticRequests` / `nudgeBypassed` observation path
  // via allowSyntheticContinuations(); PR2 should rewrite this canary to use
  // an explicit `expectSyntheticTodoNudge()` + `expectNoMoreRequests()`.

  {
    name: 'OC-NUDGE-001 nudge fires exactly once after human turn completed',
    fn: async (t) => {
      const sess = await t.client.createSession({
        id: 'test-model', providerID: 'test', limit: { input: 100000, context: 100000 },
      });
      const sid = getSessionId(sess);
      t.provider.expectToolCall({ id: 'nudge-todo', tool: 'todowrite', args: {
        todos: [{ content: 'pending nudge task', status: 'pending', priority: 'high' }],
        select_methodology: ['first_principles'],
      } });
      t.provider.expectText({ id: 'nudge-todo-text', text: 'continue' });
      t.provider.expectNoMoreRequests();
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'write a todo and say continue');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const deadline = Date.now() + TIMEOUTS.idleNudge;
      while (!t.provider.syntheticRequests.some((r) => r.marker === 'todo-nudge')) {
        if (Date.now() > deadline) throw new Error('Todo-nudge did not fire within timeout');
        await sleep(200);
      }
      const nudgeReqs = t.provider.syntheticRequests.filter((r) => r.marker === 'todo-nudge');
      if (nudgeReqs.length !== 1) throw new Error(`Expected 1 todo-nudge, got ${nudgeReqs.length}`);
      const nudge = nudgeReqs[0];
      const nudgeMsg = nudge.body?.messages?.[nudge.body.messages.length - 1];
      const nudgeText = typeof nudgeMsg?.content === 'string' ? nudgeMsg.content : JSON.stringify(nudgeMsg?.content);
      if (!nudgeText.includes('There are still incomplete todos')) {
        throw new Error('Todo-nudge marker text not found: ' + nudgeText);
      }
      if (t.provider.nudgeBypassed < 1) throw new Error('nudgeBypassed should be >= 1');
      expectNoSessionError(t, sid);
    },
  },

  // OC-SUB-001 / OC-SUB-005: coder tool call creates a real child session;
  // coder final output arrives as a tool result in the parent messages.
  // PR2 will assert the child session ID, parentID linkage, agent, model,
  // and registry cleanup. PR1 keeps the original tool-name + message-oracle check.

  {
    name: 'OC-SUB-001 OC-SUB-005 coder child session round-trip',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      const coderResultText = 'coder mock execution output';
      t.provider.expectToolCall({ id: 'coder-call', tool: 'coder', args: { intents: [], tdd: 'green' } });
      t.provider.expectText({ id: 'coder-child-result', text: coderResultText });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'run coder to implement a feature');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
      const firstReq = t.provider.requests[0];
      if (!firstReq) throw new Error('No LLM request recorded');
      const toolNames = extractToolNames(firstReq.tools);
      if (!toolNames.includes('coder')) throw new Error('coder tool not called');
      const msgsStr = JSON.stringify((await t.client.messages(sid)).data);
      if (!msgsStr.includes(coderResultText)) throw new Error('Coder tool result not in messages');
      expectNoSessionError(t, sid);
    },
  },

];

export default tests;
