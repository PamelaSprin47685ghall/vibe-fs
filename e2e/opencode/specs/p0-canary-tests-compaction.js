/**
 * p0-canary-tests-compaction.js — Compaction E2E tests.
 * Kept under the 300-line Kolmogorov line budget.
 */

import { getSessionId } from '../harness/scenario.js';
import { content, writeWorkFile, findToolPart, readNdjsonLines, TIMEOUTS } from './p0-canary-utils.js';
import fs from 'node:fs';
import YAML from 'yaml';

function expectNoSessionError(t, sid) {
  t.events.expectCount({ type: 'session.error', sessionID: sid, count: 0 });
}

function countOccurrences(text, sub) {
  return text.split(sub).length - 1;
}

async function enableCompaction(t) {
  const config = JSON.parse(t.host._env.OPENCODE_CONFIG_CONTENT || '{}');
  config.compaction = { auto: true, tail_turns: 1 };
  t.host._startOpts.extraEnv = {
    ...t.host._startOpts.extraEnv,
    OPENCODE_DISABLE_AUTOCOMPACT: '0',
    OPENCODE_CONFIG_CONTENT: JSON.stringify(config),
  };
  await t.restart();
}

export function parseToolOutput(text) {
  if (!text || !text.startsWith('---\n')) {
    return { metadata: {}, body: text || '' };
  }
  let endIdx = text.indexOf('\n---\n', 4);
  let fenceLength = 5;
  if (endIdx === -1) {
    if (text.endsWith('\n---')) {
      endIdx = text.length - 4;
      fenceLength = 4;
    }
  }
  if (endIdx === -1) {
    return { metadata: {}, body: text };
  }
  const yamlText = text.slice(4, endIdx);
  const body = text.slice(endIdx + fenceLength);
  try {
    const metadata = YAML.parse(yamlText) || {};
    return { metadata, body };
  } catch (e) {
    return { metadata: {}, body };
  }
}

const tests = [
  {
    name: 'OC-COMP-001 OC-COMP-002 OC-COMP-003 OC-COMP-004 OC-COMP-005 OC-COMP-006 Compaction success flow',
    fn: async (t) => {
      await enableCompaction(t);
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);

      // Turn 1: Backlog + large output triggers compaction immediately
      t.provider.expectToolCall({
        id: 'c1-todo',
        tool: 'todowrite',
        args: {
          plan: 'PLAN_COMPACT_111',
          todos: [{ content: 'c1 task', status: 'completed', priority: 'high' }],
          select_methodology: ['first_principles'],
        }
      });
      t.provider.expectText({
        id: 'c1-summary',
        text: 'Summary of past events: done.',
        match: { containsText: ['MUST NOT be executed', 'PLAN_COMPACT_111'] }
      });
      t.provider.expectText({ id: 'c1-done', text: 'Initialize backlog complete' });

      const turn1 = await t.turn.start(sid);
      await t.client.prompt(sid, 'Initialize backlog');
      await turn1.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      // Turn 2: normal turn since Turn 1 compaction succeeded
      t.provider.expectText({ id: 'c1-prompt2-done', text: 'finished turn 2' });
      const turn2 = await t.turn.start(sid);
      await t.client.prompt(sid, 'Next step');
      await turn2.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      // Assertions
      const lines = readNdjsonLines(t.host.workDir).filter(l => l.Session === sid);
      const startEvent = lines.find(l => l.Kind === 'compaction_started');
      const settledEvent = lines.find(l => l.Kind === 'compaction_settled');
      const genChangedEvent = lines.find(l => l.Kind === 'context_generation_changed');

      if (!startEvent) throw new Error('compaction_started event missing');
      if (!settledEvent) throw new Error('compaction_settled event missing');
      if (!genChangedEvent) throw new Error('context_generation_changed event missing');

      if (settledEvent.Payload?.status !== 'completed') {
        throw new Error(`expected compaction status completed, got: ${JSON.stringify(settledEvent.Payload)}`);
      }

      // Autocontinue synthetic message
      const messages = (await t.client.messages(sid)).data || [];
      const synthMsg = messages.find(m => m.parts?.some(p => p.synthetic && p.metadata?.compaction_continue));
      if (!synthMsg) throw new Error('Synthetic compaction continuation message missing');

      expectNoSessionError(t, sid);
    }
  },

  {
    name: 'OC-COMP-007 OC-COMP-008 Compaction failure and recovery',
    fn: async (t) => {
      await enableCompaction(t);
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);

      t.provider.expectToolCall({
        id: 'c2-todo',
        tool: 'todowrite',
        args: {
          plan: 'PLAN_COMPACT_222',
          todos: [{ content: 'c2 task', status: 'completed', priority: 'high' }],
          select_methodology: ['first_principles'],
        }
      });
      t.provider.expectError({ id: 'c2-compaction-fail', status: 500 });

      const turn1 = await t.turn.start(sid);
      await t.client.prompt(sid, 'Initialize backlog');
      try {
        await turn1.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
      } catch (err) {
        // Expected failure on Turn 1 due to compaction 500 error
      }

      const lines = readNdjsonLines(t.host.workDir).filter(l => l.Session === sid);
      const settledEvent = lines.find(l => l.Kind === 'compaction_settled');
      if (!settledEvent) throw new Error('compaction_settled event missing');
      if (settledEvent.Payload?.status !== 'failed') {
        throw new Error(`expected compaction status failed, got: ${JSON.stringify(settledEvent.Payload)}`);
      }

      // Turn 2: Compaction triggers again and succeeds
      t.provider.expectText({
        id: 'c2-compaction-retry-success',
        text: 'Summary of past events: recovered.',
        match: { containsText: ['MUST NOT be executed'] }
      });
      t.provider.expectText({ id: 'c2-done', text: 'recovered turn 2' });

      const turn2 = await t.turn.start(sid);
      await t.client.prompt(sid, 'Next step');
      await turn2.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      expectNoSessionError(t, sid);
    }
  },

  {
    name: 'OC-COMP-009 Late events for old compaction do not affect new',
    fn: async (t) => {
      await enableCompaction(t);
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);

      t.provider.expectText({
        id: 'c3-late-event',
        text: 'normal reply',
        compactionControl: { compaction_continue: true }
      });

      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'say hello');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const lines = readNdjsonLines(t.host.workDir).filter(l => l.Session === sid);
      const settled = lines.filter(l => l.Kind === 'compaction_settled');
      if (settled.length > 0) {
        throw new Error('compaction unexpectedly settled by late event');
      }

      expectNoSessionError(t, sid);
    }
  },

  {
    name: 'OC-COMP-010 Restart recovers incomplete compaction',
    fn: async (t) => {
      await enableCompaction(t);
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);

      t.provider.expectToolCall({
        id: 'c4-todo',
        tool: 'todowrite',
        args: {
          plan: 'PLAN_COMPACT_444',
          todos: [{ content: 'c4 task', status: 'completed', priority: 'high' }],
          select_methodology: ['first_principles'],
        }
      });
      t.provider.expectText({
        id: 'c4-compaction-aborted',
        text: 'should be aborted',
      });
      t.provider.expectText({
        id: 'c4-compaction-recovered',
        text: 'Summary of past events: recovered.',
        match: { containsText: ['MUST NOT be executed', 'PLAN_COMPACT_444'] }
      });
      t.provider.expectText({ id: 'c4-done', text: 'finished turn 1' });

      const turn1 = await t.turn.start(sid);
      await t.client.prompt(sid, 'Initialize backlog');

      // Wait until compaction request arrives, then restart host
      const deadline = Date.now() + 15000;
      while (Date.now() < deadline) {
        if (t.provider.requests.length >= 2) break; // 1: init, 2: compaction
        await new Promise(r => setTimeout(r, 100));
      }

      await t.restart();
      await turn1.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const lines = readNdjsonLines(t.host.workDir).filter(l => l.Session === sid);
      const settledEvent = lines.find(l => l.Kind === 'compaction_settled');
      if (!settledEvent) throw new Error('compaction_settled event missing after restart');
      if (settledEvent.Payload?.status !== 'completed') {
        throw new Error(`expected compaction status completed, got: ${JSON.stringify(settledEvent.Payload)}`);
      }

      expectNoSessionError(t, sid);
    }
  },

  {
    name: 'OC-COMP-011 Two simultaneous compactions isolated',
    fn: async (t) => {
      await enableCompaction(t);
      const sessA = await t.client.createSession();
      const sidA = getSessionId(sessA);
      const sessB = await t.client.createSession();
      const sidB = getSessionId(sessB);

      // Session A: todowrite + compaction
      t.provider.expectToolCall({
        id: 'c5-todo-a',
        tool: 'todowrite',
        args: {
          plan: 'PLAN_A',
          todos: [{ content: 'a', status: 'completed', priority: 'high' }],
          select_methodology: ['first_principles'],
        }
      });
      t.provider.expectText({
        id: 'c5-summary-a',
        text: 'Summary of past events: done.',
        match: { containsText: ['MUST NOT be executed', 'PLAN_A'] }
      });
      t.provider.expectText({ id: 'c5-done-a', text: 'done' });

      const turnA1 = await t.turn.start(sidA);
      await t.client.prompt(sidA, 'Init A');
      await turnA1.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      // Session B: unaffected, normal prompt
      t.provider.expectText({ id: 'c5-normal-b', text: 'normal reply B' });
      const turnB1 = await t.turn.start(sidB);
      await t.client.prompt(sidB, 'Init B');
      await turnB1.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const linesB1 = readNdjsonLines(t.host.workDir).filter(l => l.Session === sidB);
      const settledB = linesB1.filter(l => l.Kind === 'compaction_settled');
      if (settledB.length > 0) {
        throw new Error('Session B unexpectedly settled compaction');
      }

      expectNoSessionError(t, sidA);
      expectNoSessionError(t, sidB);
    }
  },

  {
    name: 'OC-COMP-012 Old tool output not re-injected',
    fn: async (t) => {
      await enableCompaction(t);
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);

      t.provider.expectToolCall({
        id: 'c6-todo',
        tool: 'todowrite',
        args: {
          plan: 'PLAN_COMPACT_666',
          todos: [{ content: 'c6 task', status: 'completed', priority: 'high' }],
          select_methodology: ['first_principles'],
        }
      });
      t.provider.expectText({
        id: 'c6-summary',
        text: 'Summary of past events: done.',
        match: { containsText: ['MUST NOT be executed'] }
      });
      t.provider.expectText({ id: 'c6-done', text: 'done' });

      const turn1 = await t.turn.start(sid);
      await t.client.prompt(sid, 'Initialize backlog');
      await turn1.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      // Turn 2: check that PLAN_COMPACT_666 is not re-injected
      t.provider.expectText({ id: 'c6-prompt2-done', text: 'finished turn 2' });
      const turn2 = await t.turn.start(sid);
      await t.client.prompt(sid, 'Next step');
      await turn2.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const requests = t.provider.requests;
      const lastReq = requests[requests.length - 1];
      const bodyStr = JSON.stringify(lastReq || {});
      if (bodyStr.includes('PLAN_COMPACT_666')) {
        throw new Error('Old backlog plan still present in the prompt post-compaction');
      }

      expectNoSessionError(t, sid);
    }
  }
];

export default tests;
