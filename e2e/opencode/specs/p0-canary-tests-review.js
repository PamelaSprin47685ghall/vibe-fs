/**
 * p0-canary-tests-review.js — Review specs covering OC-REV-001..007, 015, 016.
 * Compacted to keep under 300 lines.
 */

import fs from 'node:fs';
import { getSessionId } from '../../../testkit/opencode/scenario.js';
import { readNdjsonLines, sleep, TIMEOUTS } from './p0-canary-utils.js';

function expectNoSessionError(t, sid) {
  t.events.expectCount({ type: 'session.error', sessionID: sid, count: 0 });
}

async function waitForPlugin(client, timeoutMs = 10000) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    const res = await client.request('GET', '/command');
    if (res.ok && Array.isArray(res.data)) {
      const names = res.data.map((c) => c.name);
      if (names.includes('loop')) return;
    }
    await new Promise((r) => setTimeout(r, 100));
  }
  throw new Error('Plugin failed to load within timeout');
}

async function setup(t) {
  await waitForPlugin(t.client);
  const sess = await t.client.createSession();
  const sid = getSessionId(sess);
  t.provider.expectText({ id: 'rev-warm', text: 'ok' });
  const turn = await t.turn.start(sid);
  await t.client.prompt(sid, 'hello');
  await turn.awaitTerminal({ timeoutMs: TIMEOUTS.quick });
  return sid;
}

const tests = [
  {
    name: 'OC-REV-001 OC-REV-002 OC-REV-003 /loop task activates review mode',
    fn: async (t) => {
      const sid = await setup(t);
      const cmdTurn = await t.turn.start(sid);
      const cmdRes = await t.client.runCommand(sid, 'loop', 'implement feature X', 10000);
      if (!cmdRes.ok) throw new Error(`loop command failed: ${cmdRes.status}`);
      await cmdTurn.awaitTerminal({ timeoutMs: TIMEOUTS.quick, requireAssistantTerminal: false });

      const loopEntries = readNdjsonLines(t.host.workDir).filter(
        (line) => line.Kind === 'loop_activated' && line.Session === sid,
      );
      if (loopEntries.length !== 1) throw new Error(`expected exactly one loop_activated, got ${loopEntries.length}`);
      const entry = loopEntries[0];
      if (entry.Payload?.task !== 'implement feature X') {
        throw new Error(`loop_activated payload mismatch: ${JSON.stringify(entry.Payload)}`);
      }
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-REV-004 second /loop returns already active',
    fn: async (t) => {
      const sid = await setup(t);
      const cmdTurn1 = await t.turn.start(sid);
      await t.client.runCommand(sid, 'loop', 'implement feature X', 10000);
      await cmdTurn1.awaitTerminal({ timeoutMs: TIMEOUTS.quick, requireAssistantTerminal: false });

      const cmdTurn2 = await t.turn.start(sid);
      const cmdRes = await t.client.runCommand(sid, 'loop', 'implement feature Y', 10000);
      await cmdTurn2.awaitTerminal({ timeoutMs: TIMEOUTS.quick, requireAssistantTerminal: false });

      const text = JSON.stringify(cmdRes.data);
      if (!text.includes('already active') && !text.includes('already_active') && !text.includes('Review loop is already active')) {
        throw new Error(`Expected already active error, got: ${text}`);
      }
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-REV-005 empty /loop cancels loop and appends loop_cancelled',
    fn: async (t) => {
      const sid = await setup(t);
      const cmdTurn1 = await t.turn.start(sid);
      await t.client.runCommand(sid, 'loop', 'implement feature X', 10000);
      await cmdTurn1.awaitTerminal({ timeoutMs: TIMEOUTS.quick, requireAssistantTerminal: false });

      const cmdTurn2 = await t.turn.start(sid);
      const cmdRes = await t.client.runCommand(sid, 'loop', '', 10000);
      await cmdTurn2.awaitTerminal({ timeoutMs: TIMEOUTS.quick, requireAssistantTerminal: false });

      const text = JSON.stringify(cmdRes.data);
      if (!text.includes('cancelled') && !text.includes('With-Review Mode has been cancelled')) {
        throw new Error(`Expected cancelled message, got: ${text}`);
      }

      const cancelEntries = readNdjsonLines(t.host.workDir).filter(
        (line) => line.Kind === 'loop_cancelled' && line.Session === sid,
      );
      if (cancelEntries.length === 0) throw new Error('loop_cancelled NDJSON entry not found');
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-REV-006 submit_review with no active loop is rejected',
    fn: async (t) => {
      const sid = await setup(t);
      t.provider.expectToolCall({
        id: 'sr',
        tool: 'submit_review',
        args: { report: 'final report', affectedFiles: ['a.txt'], wip: false }
      });
      t.provider.expectText({ id: 'parent-done', text: 'done' });

      const turn2 = await t.turn.start(sid);
      await t.client.prompt(sid, 'submit review');
      await turn2.awaitTerminal({ timeoutMs: TIMEOUTS.quick, requireAssistantTerminal: false });

      const messages = (await t.client.messages(sid)).data || [];
      const msgsStr = JSON.stringify(messages);
      if (!msgsStr.includes('not active') && !msgsStr.includes('No review needed') && !msgsStr.includes('submitReviewNotNeeded')) {
        throw new Error(`Expected not active error in messages, got: ${msgsStr}`);
      }
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-REV-007 submit_review creates child reviewer session and receives verdict',
    fn: async (t) => {
      const sid = await setup(t);
      const cmdTurn = await t.turn.start(sid);
      await t.client.runCommand(sid, 'loop', 'implement feature X', 10000);
      await cmdTurn.awaitTerminal({ timeoutMs: TIMEOUTS.quick, requireAssistantTerminal: false });

      t.provider.expectToolCall({
        id: 'sr',
        tool: 'submit_review',
        args: { report: 'final report', affectedFiles: ['a.txt'], wip: false }
      });
      t.provider.expectToolCall({
        id: 'rr',
        tool: 'return_reviewer',
        args: { verdict: 'REVISE', feedback: 'please fix bugs' }
      });
      t.provider.expectText({ id: 'parent-done', text: 'done' });

      const turn2 = await t.turn.start(sid);
      await t.client.prompt(sid, 'submit review');
      await turn2.awaitTerminal({ timeoutMs: TIMEOUTS.prompt, requireAssistantTerminal: false });

      const ndjson = readNdjsonLines(t.host.workDir);
      const spawnEntry = ndjson.find(
        (line) => line.Kind === 'subagent_spawned' && line.Session === sid && line.Payload?.agent === 'reviewer',
      );
      if (!spawnEntry) throw new Error('subagent_spawned event not found or agent is not reviewer');
      if (spawnEntry.Payload?.title !== 'Reviewer') throw new Error(`expected Reviewer, got: ${spawnEntry.Payload?.title}`);

      const verdictEntry = ndjson.find((line) => line.Kind === 'review_verdict' && line.Session === sid);
      if (!verdictEntry) throw new Error('review_verdict event not found');
      if (verdictEntry.Payload?.verdict !== 'needs_revision') throw new Error(`expected needs_revision, got: ${verdictEntry.Payload?.verdict}`);
      if (verdictEntry.Payload?.feedback !== 'please fix bugs') throw new Error(`expected feedback 'please fix bugs', got: ${verdictEntry.Payload?.feedback}`);
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-REV-015 submit_review with wip=true records progress but does not spawn child',
    fn: async (t) => {
      const sid = await setup(t);
      const cmdTurn = await t.turn.start(sid);
      await t.client.runCommand(sid, 'loop', 'implement feature X', 10000);
      await cmdTurn.awaitTerminal({ timeoutMs: TIMEOUTS.quick, requireAssistantTerminal: false });

      t.provider.expectToolCall({
        id: 'sr-wip',
        tool: 'submit_review',
        args: { report: 'wip report', affectedFiles: [], wip: true }
      });
      t.provider.expectText({ id: 'parent-done', text: 'done' });

      const turn2 = await t.turn.start(sid);
      await t.client.prompt(sid, 'submit wip review');
      await turn2.awaitTerminal({ timeoutMs: TIMEOUTS.prompt, requireAssistantTerminal: false });

      const ndjson = readNdjsonLines(t.host.workDir);
      const wipEntry = ndjson.find((line) => line.Kind === 'submit_review_wip_recorded' && line.Session === sid);
      if (!wipEntry) throw new Error('submit_review_wip_recorded event not found');
      if (wipEntry.Payload?.report !== 'wip report') throw new Error(`expected 'wip report', got: ${wipEntry.Payload?.report}`);

      const spawnEntry = ndjson.find((line) => line.Kind === 'subagent_spawned' && line.Session === sid && line.Payload?.agent === 'reviewer');
      if (spawnEntry) throw new Error('Reviewer child session was unexpectedly spawned for wip=true');
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-REV-016 parent abort cancels child reviewer',
    fn: async (t) => {
      const sid = await setup(t);
      const cmdTurn = await t.turn.start(sid);
      await t.client.runCommand(sid, 'loop', 'implement feature X', 10000);
      await cmdTurn.awaitTerminal({ timeoutMs: TIMEOUTS.quick, requireAssistantTerminal: false });

      t.provider.expectToolCall({
        id: 'sr',
        tool: 'submit_review',
        args: { report: 'final report', affectedFiles: ['a.txt'], wip: false }
      });
      t.provider.expectToolCall({
        id: 'rr-delayed',
        tool: 'return_reviewer',
        args: { verdict: 'REVISE', feedback: 'bugs' },
        delayFirstToken: 5000
      });

      const turn2 = await t.turn.start(sid);
      await t.client.prompt(sid, 'submit review');

      const deadline = Date.now() + 10000;
      let childSid = null;
      while (Date.now() < deadline) {
        const ndjson = readNdjsonLines(t.host.workDir);
        const spawnEntry = ndjson.find((line) => line.Kind === 'subagent_spawned' && line.Session === sid && line.Payload?.agent === 'reviewer');
        if (spawnEntry) {
          childSid = spawnEntry.Payload?.childId;
          break;
        }
        await sleep(200);
      }
      if (!childSid) throw new Error('Reviewer child session not spawned within timeout');

      const abortRes = await t.client.abort(sid);
      if (!abortRes.ok) throw new Error(`abort parent failed: ${abortRes.status}`);

      await turn2.awaitTerminal({ timeoutMs: 15000, requireAssistantTerminal: false });

      const ndjson = readNdjsonLines(t.host.workDir);
      const verdictEntry = ndjson.find((line) => line.Kind === 'review_verdict' && line.Session === sid);
      if (verdictEntry) throw new Error('review_verdict was unexpectedly written after parent abort');

      const abortEntry = ndjson.find((line) => line.Kind === 'user_abort_observed' && line.Session === sid);
      if (!abortEntry) throw new Error('user_abort_observed event not found');
    },
  },
];

export default tests;
