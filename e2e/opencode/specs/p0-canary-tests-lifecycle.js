/**
 * p0-canary-tests-lifecycle.js — OpenCode session lifecycle P0 E2E canary.
 *
 * Covers OC-LIFE-001..015 with real, externally observable assertions.
 * Uses EventProbe await helpers and scenario harness; no fixed sleeps.
 */

import { getSessionId, runScenario } from '../harness/scenario.js';
import { isPidAlive, getDescendantPids } from '../harness/process-host-checks.js';
import {
  content,
  findToolPart,
  parsePtySpawnOutput,
  readNdjsonLines,
  TIMEOUTS,
} from './p0-canary-utils.js';
import { waitForCondition } from './p0-canary-ndjson.js';

const COMMON = {
  plugin: true,
  timeoutMs: 120000,
  contextLimit: 20000,
  allowSynthetic: true,
  allowTitleGen: true,
};

function expectNoSessionError(t, sid) {
  t.events.expectCount({ type: 'session.error', sessionID: sid, count: 0 });
}

function statusType(e) {
  return e.properties?.status?.type || e.properties?.status?.status || e.properties?.status;
}

function isIdle(e, sid) {
  return e.sessionID === sid && (e.type === 'session.idle' || (e.type === 'session.status' && statusType(e) === 'idle'));
}

function isError(e, sid) {
  return e.sessionID === sid && e.type === 'session.error';
}

function isTerminal(e, sid) {
  return isIdle(e, sid) || isError(e, sid);
}

function isToolCallFinish(e, sid) {
  return e.sessionID === sid && e.type === 'message.updated' && /tool[-_]calls?/.test(String(e.finishReason || ''));
}

function bySessionType(events, sid, type) {
  return events.filter((e) => e.sessionID === sid && e.type === type);
}

async function waitForAsyncCondition(read, predicate, timeoutMs) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() <= deadline) {
    const value = await read();
    if (predicate(value)) return value;
    await new Promise((resolve) => setImmediate(resolve));
  }
  throw new Error(`Timed out waiting for async condition after ${timeoutMs}ms`);
}

async function getProcessCmds(pid) {
  const pids = await getDescendantPids(pid);
  if (pids.length === 0) return [];
  const { execSync } = await import('node:child_process');
  const out = execSync(`ps -p ${pids.join(',')} -o cmd= 2>/dev/null || true`).toString().trim();
  return out.split(/\r?\n/).map((s) => s.trim()).filter(Boolean);
}

const tests = [
  {
    name: 'OC-LIFE-001 new human message is recorded as a human-turn fact',
    fn: async (t) => {
      const sid = getSessionId(await t.client.createSession());
      const token = `human-token-${Date.now()}`;
      t.provider.expectText({ id: 'life-001', text: 'ack' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, token);
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
      const msgs = (await t.client.messages(sid)).data || [];
      const userMsgs = msgs.filter((m) => m.info?.role === 'user');
      if (!userMsgs.some((m) => JSON.stringify(m.parts || []).includes(token))) {
        throw new Error('new human message was not recorded as a human-turn fact');
      }
      if (userMsgs.length !== 1) {
        throw new Error(`expected exactly one human turn, got ${userMsgs.length}`);
      }
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-LIFE-002 internal nudge is not recorded as a human turn',
    fn: async (t) => {
      const sid = getSessionId(await t.client.createSession());
      t.provider.expectToolCall({
        id: 'life-002-todo',
        tool: 'todowrite',
        args: {
          todos: [{ content: 'nudge check', status: 'pending', priority: 'high' }],
          select_methodology: ['first_principles'],
        },
      });
      t.provider.expectText({ id: 'life-002-text', text: 'continue' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'write a todo and say continue');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
      const msgs = (await t.client.messages(sid)).data || [];
      const userMsgs = msgs.filter((m) => m.info?.role === 'user');
      if (userMsgs.length !== 1) {
        throw new Error(`internal nudge counted as a human turn: ${userMsgs.length} user messages`);
      }
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-LIFE-003 child nonce message is not recorded as a human turn',
    fn: async (t) => {
      const sid = getSessionId(await t.client.createSession());
      const nonce = 'child-nonce-003';
      t.provider.expectToolCall({ id: 'life-003-coder', tool: 'coder', args: { intents: [], tdd: 'green' } });
      t.provider.expectText({ id: 'life-003-result', text: nonce });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'use coder');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
      const msgs = (await t.client.messages(sid)).data || [];
      const userMsgs = msgs.filter((m) => m.info?.role === 'user');
      if (userMsgs.length !== 1) {
        throw new Error(`child nonce counted as a human turn: ${userMsgs.length} user messages`);
      }
      if (!JSON.stringify(msgs).includes(nonce)) {
        throw new Error('child result did not reach parent messages');
      }
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-LIFE-004 normal round transitions from busy to terminal idle',
    fn: async (t) => {
      const sid = getSessionId(await t.client.createSession());
      t.provider.expectText({ id: 'life-004', text: 'ok' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'say ok');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
      const all = t.events.allEvents;
      const statuses = bySessionType(all, sid, 'session.status');
      if (!statuses.some((e) => statusType(e) && statusType(e) !== 'idle')) {
        throw new Error('expected a non-idle session.status event');
      }
      const finished = bySessionType(all, sid, 'message.updated').filter((e) => Boolean(e.finishReason));
      if (finished.length < 1) throw new Error(`expected terminal message, got ${finished.length}`);
      if (t.events.count('session.idle', sid) !== 1) {
        throw new Error(`expected exactly one session.idle, got ${t.events.count('session.idle', sid)}`);
      }
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-LIFE-005 API error round transitions to idle',
    fn: async (t) => {
      const sid = getSessionId(await t.client.createSession());
      // Use non-retryable 400 so the host does not auto-retry and drain the mock queue.
      t.provider.expectError({
        id: 'life-005',
        status: 400,
        body: { error: { name: 'APIError', message: 'invalid request', type: 'invalid_request_error', isRetryable: false } },
      });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'trigger error');
      const err = await t.events.awaitEvent((e) => isError(e, sid), TIMEOUTS.prompt);
      await t.events.awaitEvent((e) => isIdle(e, sid) && e.seq >= err.seq, TIMEOUTS.prompt);
      const statuses = t.events.bySession(sid).filter((e) => e.type === 'session.status');
      const last = statusType(statuses[statuses.length - 1]);
      if (last !== 'idle' && t.events.count('session.idle', sid) < 1) {
        throw new Error(`expected idle after API error, last status=${last}`);
      }
      if (t.provider.unexpectedRequests.length !== 0) {
        throw new Error(`host retried after non-retryable error: ${t.provider.unexpectedRequests.length}`);
      }
    },
  },

  {
    name: 'OC-LIFE-006 abort produces MessageAbortedError or terminal idle',
    fn: async (t) => {
      const sid = getSessionId(await t.client.createSession());
      t.provider.strict = false;
      t.provider.expectToolCall({
        id: 'life-006-exec',
        tool: 'executor',
        args: { language: 'shell', command: 'sleep 30', mode: 'rw', what_to_summarize: 'abort test', max_bytes: 100 },
      });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'run sleep 30');
      await t.events.awaitEvent((e) => isToolCallFinish(e, sid), 15000);
      await t.client.abort(sid);
      const terminal = await t.events.awaitEvent((e) => isTerminal(e, sid), 15000);
      if (terminal.type === 'session.error') {
        const ok = ['MessageAbortedError', 'AbortError'];
        if (!ok.includes(terminal.errorName)) throw new Error(`unexpected abort error name: ${terminal.errorName}`);
      }
    },
  },

  {
    name: 'OC-LIFE-007 abort terminates pending executor child process',
    fn: async (t) => {
      const sid = getSessionId(await t.client.createSession());
      t.provider.strict = false;
      const marker = 'life-007.marker';
      const cmd = `touch ${marker} && sleep 30`;
      t.provider.expectToolCall({
        id: 'life-007-exec',
        tool: 'executor',
        args: { language: 'shell', command: cmd, mode: 'rw', what_to_summarize: 'abort child test', max_bytes: 100 },
      });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, `run ${cmd}`);
      await waitForCondition(() => {
        try { return require('node:fs').existsSync(`${t.host.workDir}/${marker}`); } catch { return false; }
      }, (x) => x, 15000);
      const before = await getProcessCmds(t.host.pid);
      if (!before.some((c) => c.includes('sleep 30'))) throw new Error('executor child not running before abort');
      await t.client.abort(sid);
      await t.events.awaitEvent((e) => isTerminal(e, sid), 15000);
      const deadline = Date.now() + 10000;
      let cmds = before;
      while (Date.now() < deadline) {
        cmds = await getProcessCmds(t.host.pid);
        if (!cmds.some((c) => c.includes('sleep 30'))) break;
        await new Promise((r) => setImmediate(r));
      }
      if (cmds.some((c) => c.includes('sleep 30'))) {
        throw new Error('executor child still alive after abort');
      }
    },
  },

  {
    name: 'OC-LIFE-008 tool input already issued is not erased by abort',
    fn: async (t) => {
      const sid = getSessionId(await t.client.createSession());
      t.provider.strict = false;
      t.provider.expectToolCall({
        id: 'life-008-exec',
        tool: 'executor',
        args: { language: 'shell', command: 'echo life-008 && sleep 30', mode: 'rw', what_to_summarize: 'keep test', max_bytes: 100 },
      });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'run echo life-008 and sleep 30');
      await t.events.awaitEvent((e) => isToolCallFinish(e, sid), 15000);
      await t.client.abort(sid);
      await t.events.awaitEvent((e) => isTerminal(e, sid), 15000);
      const msgs = (await t.client.messages(sid)).data || [];
      const toolPart = findToolPart(msgs, 'executor');
      if (!toolPart) throw new Error('executor tool part missing from messages after abort');
      if (!JSON.stringify(toolPart.state?.input || {}).includes('life-008')) {
        throw new Error('executor tool input erased by abort');
      }
    },
  },

  {
    name: 'OC-LIFE-009 abort during stream leaves at most one terminal state',
    fn: async (t) => {
      const sid = getSessionId(await t.client.createSession());
      t.provider.expectText({ id: 'life-009', text: 'keep talking', delayDone: 5000 });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'speak for a while');
      await t.events.awaitEvent((e) => e.sessionID === sid && e.type === 'message.part.updated', 10000);
      await t.client.abort(sid);
      await t.events.awaitEvent((e) => isTerminal(e, sid), 15000);
      const all = t.events.bySession(sid);
      const terminalCount = all.filter((e) => isTerminal(e, sid)).length;
      if (terminalCount > 1) throw new Error(`expected ≤1 terminal state after abort, got ${terminalCount}`);
    },
  },

  {
    name: 'OC-LIFE-010 repeated idle is idempotent per turn',
    fn: async (t) => {
      const sid = getSessionId(await t.client.createSession());
      t.provider.expectText({ id: 'life-010', text: 'one' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'say one');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
      // Force an explicit idle query; the event stream must not emit a duplicate session.idle.
      await t.client.sessionStatus(sid);
      const idleCount = t.events.count('session.idle', sid);
      if (idleCount !== 1) throw new Error(`expected exactly one session.idle, got ${idleCount}`);
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-LIFE-011 session deleted cleans spawned PTY and runtime state',
    fn: async (t) => {
      t.provider.strict = false;
      const sid = getSessionId(await t.client.createSession());
      t.provider.expectToolCall({
        id: 'life-011-pty',
        tool: 'pty_spawn',
        args: { command: 'sleep', args: ['5'], description: 'lifecycle delete cleanup' },
      });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'spawn a PTY running sleep 5');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt, requireIdleAfterActivity: false });
      const spawnPart = await waitForAsyncCondition(
        async () => {
          const msgs = (await t.client.messages(sid)).data || [];
          const part = findToolPart(msgs, 'pty_spawn');
          if (!part) return null;
          const info = parsePtySpawnOutput(part.state?.output || '');
          return info.status === 'running' ? part : null;
        },
        (part) => part !== null,
        TIMEOUTS.quick,
      );
      if (!spawnPart) throw new Error('pty_spawn did not become running');
      const { id, pid } = parsePtySpawnOutput(spawnPart.state?.output || '');
      if (!id || !pid) throw new Error(`pty_spawn missing id/pid: ${JSON.stringify(spawnPart)}`);
      if (!isPidAlive(pid)) throw new Error(`PTY process ${pid} should be running before delete`);
      const delRes = await t.client.request('DELETE', `/session/${sid}`);
      if (!delRes.ok) throw new Error(`DELETE failed: ${delRes.status}`);
      await t.events.awaitEvent((e) => e.type === 'session.deleted' && e.sessionID === sid, 15000);
      await waitForCondition(() => isPidAlive(pid), (alive) => !alive, TIMEOUTS.quick);
      const afterStatus = await t.client.sessionStatus(sid);
      if (afterStatus.ok) throw new Error('session still accessible after delete');
    },
  },

  {
    name: 'OC-LIFE-012 delete session A does not affect session B',
    fn: async (t) => {
      const sidA = getSessionId(await t.client.createSession());
      const sidB = getSessionId(await t.client.createSession());
      t.provider.expectText({ id: 'life-012-a', text: 'ping A' });
      const turnA = await t.turn.start(sidA);
      await t.client.prompt(sidA, 'say ping A');
      await turnA.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
      const delA = await t.client.request('DELETE', `/session/${sidA}`);
      if (!delA.ok) throw new Error(`delete A failed: ${delA.status}`);
      await t.events.awaitEvent((e) => e.type === 'session.deleted' && e.sessionID === sidA, 15000);
      t.provider.expectText({ id: 'life-012-b', text: 'ping B' });
      const turnB = await t.turn.start(sidB);
      await t.client.prompt(sidB, 'say ping B');
      await turnB.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
      const statusB = await t.client.sessionStatus(sidB);
      if (!statusB.ok) throw new Error('session B was affected by deleting session A');
    },
  },

  {
    name: 'OC-LIFE-013 event hook async work is observable without fixed sleep',
    fn: async (t) => {
      const sid = getSessionId(await t.client.createSession());
      t.provider.expectText({ id: 'life-013', text: 'ok' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'say ok');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
      await waitForAsyncCondition(
        () => Promise.resolve(readNdjsonLines(t.host.workDir)),
        (lines) => lines.some((l) => l.Session === sid),
        TIMEOUTS.quick,
      );
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-LIFE-014 restart does not double-recover session state',
    fn: async (t) => {
      const sid = getSessionId(await t.client.createSession());
      t.provider.expectToolCall({ id: 'life-014-write', tool: 'write', args: { filePath: 'restart.txt', content: 'before\n' } });
      t.provider.expectText({ id: 'life-014-text', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'write restart.txt then say done');
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
      const msgsBefore = (await t.client.messages(sid)).data || [];
      const userBefore = msgsBefore.filter((m) => m.info?.role === 'user').length;
      await t.restart();
      const statusAfter = await t.client.sessionStatus(sid);
      if (!statusAfter.ok) throw new Error('session not recovered after restart');
      const msgsAfter = (await t.client.messages(sid)).data || [];
      const userAfter = msgsAfter.filter((m) => m.info?.role === 'user').length;
      if (userAfter !== userBefore) throw new Error(`user message count changed after restart: ${userBefore} -> ${userAfter}`);
      t.fs.expectFileContent('restart.txt', 'before\n');
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-LIFE-015 next prompt immediately after idle is race-free',
    fn: async (t) => {
      const sid = getSessionId(await t.client.createSession());
      t.provider.expectText({ id: 'life-015-first', text: 'one' });
      t.provider.expectText({ id: 'life-015-second', text: 'two' });
      const turn1 = await t.turn.start(sid);
      await t.client.prompt(sid, 'say one');
      await turn1.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
      const turn2 = await t.turn.start(sid);
      await t.client.prompt(sid, 'say two');
      await turn2.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
      const msgs = (await t.client.messages(sid)).data || [];
      const assistantTexts = msgs
        .filter((m) => m.info?.role === 'assistant')
        .flatMap((m) => m.parts || [])
        .filter((p) => p.type === 'text' && typeof p.text === 'string')
        .map((p) => p.text);
      if (!assistantTexts.some((s) => s.includes('one'))) throw new Error('first response missing');
      if (!assistantTexts.some((s) => s.includes('two'))) throw new Error('second response missing');
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-CONC-002 two sessions write without polluting runtime',
    fn: async (t) => {
      const sidA = getSessionId(await t.client.createSession());
      const sidB = getSessionId(await t.client.createSession());
      t.provider.expectToolCall({ id: 'write-a', tool: 'write', args: { filePath: 'a.txt', content: content('A') } });
      t.provider.expectText({ id: 'done-a', text: 'done A' });
      t.provider.expectToolCall({ id: 'write-b', tool: 'write', args: { filePath: 'b.txt', content: content('B') } });
      t.provider.expectText({ id: 'done-b', text: 'done B' });
      const turnA = await t.turn.start(sidA);
      await t.client.prompt(sidA, 'write a.txt with A');
      await turnA.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
      const turnB = await t.turn.start(sidB);
      await t.client.prompt(sidB, 'write b.txt with B');
      await turnB.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
      t.fs.expectFileContent('a.txt', content('A'));
      t.fs.expectFileContent('b.txt', content('B'));
      const msgsA = (await t.client.messages(sidA)).data || [];
      const msgsB = (await t.client.messages(sidB)).data || [];
      const strA = JSON.stringify(msgsA);
      const strB = JSON.stringify(msgsB);
      if (strA.includes('b.txt') || strA.includes('done B')) throw new Error('session A polluted by B');
      if (strB.includes('a.txt') || strB.includes('done A')) throw new Error('session B polluted by A');
      expectNoSessionError(t, sidA);
      expectNoSessionError(t, sidB);
    },
  },
];

export default tests;

if (process.argv[1] && process.argv[1].endsWith('p0-canary-tests-lifecycle.js')) {
  const only = process.argv[2];
  const toRun = only ? tests.filter((x) => x.name.includes(only)) : tests;
  const exitCode = await runScenario(COMMON, toRun);
  process.exit(exitCode);
}
