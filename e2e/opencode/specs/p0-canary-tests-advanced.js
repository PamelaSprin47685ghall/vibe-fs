/**
 * p0-canary-tests-advanced.js — Review / Web / Context-budget tests.
 * Kept under the 300-line Kolmogorov line budget.
 */

import fs from 'node:fs';
import { getSessionId } from '../harness/scenario.js';
import { extractToolNames, TIMEOUTS } from './p0-canary-utils.js';

function expectNoSessionError(t, sid) {
  t.events.expectCount({ type: 'session.error', sessionID: sid, count: 0 });
}

const tests = [

  {
    name: 'OC-REV-001 /loop task activates review mode',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectText({ id: 'rev-warm', text: 'ok' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'hello');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.quick });
      // The /loop command injects a loop-nudge synthetic continuation; in
      // PR1 it is allowed via allowSyntheticContinuations() and bypassed.
      // PR2 will add an explicit provider.expectLoopNudge() expectation.
      const cmdTurn = await t.turn.start(sid);
      const cmdRes = await t.client.runCommand(sid, 'loop', 'implement feature X', 10000);
      if (!cmdRes.ok) throw new Error(`loop command failed: ${cmdRes.status} ${cmdRes.data}`);
      await cmdTurn.awaitTerminal({ timeoutMs: TIMEOUTS.quick });
      const ndjsonPath = t.host.workDir + '/.wanxiangshu.ndjson';
      let found = false;
      if (fs.existsSync(ndjsonPath)) found = fs.readFileSync(ndjsonPath, 'utf8').includes('loop_activated');
      if (!found) found = t.events.allEvents.some((e) => e.type === 'loop_activated' || e.properties?.type === 'loop_activated');
      if (!found) throw new Error('loop_activated not found');
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-WEB-001 websearch is listed in available tools',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectText({ id: 'web-avail', text: 'tools listed' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'list your available tools');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.quick });
      const firstReq = t.provider.requests[0];
      if (!firstReq) throw new Error('No LLM request made');
      const names = extractToolNames(firstReq.tools);
      if (!names.includes('websearch')) throw new Error('websearch not in tool list');
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-CB-005 context budget nudge appears after threshold',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      const pad = 'x'.repeat(1200);
      t.provider.expectToolCall({ id: 'cb-todo', tool: 'todowrite', args: {
        ahaMoments: pad, changesAndReasons: pad, gotchas: pad,
        lessonsAndConventions: pad, plan: pad,
        todos: [{ content: 'budget test', status: 'completed', priority: 'high' }],
        select_methodology: ['first_principles'],
      } });
      // The todowrite tool result is followed by a budget-nudge synthetic
      // continuation; no separate assistant text request occurs in this turn.
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'commit a detailed report via todowrite and say continue');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
      const s = await t.client.sessionStatus(sid);
      const tokens = s.data?.data?.tokens || s.data?.tokens || {};
      if ((tokens.input || 0) < 200) throw new Error('Too few input tokens: ' + tokens.input);
      const nudges = t.provider.syntheticRequests.filter((r) => r.marker === 'budget-nudge');
      if (nudges.length === 0) throw new Error('Budget nudge synthetic not found');
      if (nudges.length > 1) throw new Error(`Expected one budget nudge, got ${nudges.length}`);
      const nudgeBody = JSON.stringify(nudges[0].body);
      if (!nudgeBody.includes('about to be')) throw new Error('Budget nudge marker text not found');
      expectNoSessionError(t, sid);
    },
  },

];

export default tests;
