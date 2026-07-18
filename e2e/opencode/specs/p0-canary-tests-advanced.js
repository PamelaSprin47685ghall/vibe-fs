/**
 * p0-canary-tests-advanced.js — Review / Web / Context-budget tests.
 * Kept under the 300-line Kolmogorov line budget.
 */

import fs from 'node:fs';
import { getSessionId } from '../harness/scenario.js';
import { extractToolNames, findToolPart, readNdjsonLines, TIMEOUTS } from './p0-canary-utils.js';

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
      if (!cmdRes.ok) throw new Error(`loop command failed: ${cmdRes.status} ${JSON.stringify(cmdRes.data)}`);
      await cmdTurn.awaitTerminal({ timeoutMs: TIMEOUTS.quick, requireAssistantTerminal: false });

      const loopEntries = readNdjsonLines(t.host.workDir).filter(
        (line) => line.Kind === 'loop_activated' && line.Session === sid,
      );
      if (loopEntries.length === 0) throw new Error('loop_activated NDJSON entry not found');
      if (loopEntries.length > 1) throw new Error(`expected exactly one loop_activated, got ${loopEntries.length}`);
      const entry = loopEntries[0];
      if (entry.Payload?.task !== 'implement feature X') {
        throw new Error(`loop_activated payload mismatch: ${JSON.stringify(entry.Payload)}`);
      }
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-WEB-001 websearch calls mock endpoint and backfills result',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectToolCall({ id: 'web-search', tool: 'websearch', args: { query: 'E2E websearch test', what_to_summarize: 'E2E websearch result', numResults: 1 } });
      t.provider.expectText({ id: 'web-summary', text: 'Test search content for E2E.' });
      t.provider.expectText({ id: 'web-done', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'search the web for E2E websearch test');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      // The executor subagent must receive the raw mock search result.
      const subagentReq = t.provider.requests[1];
      if (!subagentReq) throw new Error('websearch executor subagent request missing');
      const last = subagentReq.messages?.[subagentReq.messages.length - 1];
      const lastText = typeof last?.content === 'string' ? last.content : JSON.stringify(last?.content);
      if (!lastText.includes('Test Search Title')) throw new Error('websearch raw result title not in subagent prompt');
      if (!lastText.includes('http://example.com')) throw new Error('websearch raw result url not in subagent prompt');
      if (!lastText.includes('Test search content for E2E.')) throw new Error('websearch raw result content not in subagent prompt');

      // The main session must see the summarized tool result in messages.
      const messages = (await t.client.messages(sid)).data || [];
      const part = findToolPart(messages, 'websearch');
      if (!part) throw new Error('websearch tool part not found in messages');
      if (part.state?.status !== 'completed') throw new Error(`websearch state: ${part.state?.status}`);
      if (!part.state?.input?.query?.includes('E2E websearch test')) {
        throw new Error(`websearch query mismatch: ${part.state?.input?.query}`);
      }
      if (!part.state?.output?.includes('Test search content for E2E.')) {
        throw new Error(`websearch output mismatch: ${part.state?.output}`);
      }

      // A final assistant response must exist after the tool result.
      const assistantTexts = messages
        .filter((m) => m.info?.role === 'assistant')
        .flatMap((m) => m.parts || [])
        .filter((p) => p.type === 'text' && typeof p.text === 'string')
        .map((p) => p.text);
      if (!assistantTexts.some((text) => text.includes('done'))) {
        throw new Error('websearch did not produce final assistant response: ' + JSON.stringify(assistantTexts));
      }
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
      t.provider.expectNoMoreRequests();
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

      const nudge = nudges[0];
      const nudgeMsg = nudge.body?.messages?.[nudge.body.messages.length - 1];
      const nudgeText = typeof nudgeMsg?.content === 'string' ? nudgeMsg.content : JSON.stringify(nudgeMsg?.content);
      if (!nudgeText.includes('the system context is about to be suspended')) {
        throw new Error('Budget nudge missing marker text: ' + nudgeText);
      }
      if (!nudgeText.includes('emergency stop to all work')) {
        throw new Error('Budget nudge missing emergency stop text: ' + nudgeText);
      }

      // Threshold: input tokens must be high enough to trigger the nudge.
      if ((tokens.input || 0) < 2000) throw new Error('Input tokens too low to credibly trigger budget nudge: ' + tokens.input);
      expectNoSessionError(t, sid);
    },
  },

];

export default tests;
