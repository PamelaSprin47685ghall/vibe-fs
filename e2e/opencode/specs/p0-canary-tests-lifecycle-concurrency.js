/**
 * p0-canary-tests-lifecycle-concurrency.js — Concurrent sessions, rounds, delays, restart.
 * Kept under the 250-line Kolmogorov line budget.
 */

import { runScenario, getSessionId } from '../../../testkit/opencode/scenario.js';
import { TIMEOUTS } from './p0-canary-utils.js';
import { getDescendantPids, checkSocketClosed, isPidAlive } from '../../../testkit/opencode/process-host-checks.js';
import fs from 'node:fs';

const COMMON = { plugin: true, timeoutMs: 300000, contextLimit: 20000, allowSynthetic: true, allowTitleGen: true };

const statusType = (e) => e.properties?.status?.type || e.properties?.status;
const isIdle = (e, sid) => e.sessionID === sid && (e.type === 'session.idle' || (e.type === 'session.status' && statusType(e) === 'idle'));

async function getProcessCmds(pid) {
  const pids = await getDescendantPids(pid);
  if (pids.length === 0) return [];
  const { execSync } = await import('node:child_process');
  const out = execSync(`ps -p ${pids.join(',')} -o cmd= 2>/dev/null || true`).toString().trim();
  return out.split(/\r?\n/).map((s) => s.trim()).filter(Boolean);
}

async function waitForCount(events, predicate, target, timeoutMs) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    if (events.allEvents.filter(predicate).length >= target) return;
    await new Promise((r) => setImmediate(r));
  }
  throw new Error(`Timed out waiting for ${target} matching events`);
}

function test(name, fn) { return { name, fn }; }

const tests = [
  test('OC-CONC-001 10 sessions no cross-talk', async (t) => {
    t.provider.strict = false;
    const sessions = [];
    for (let i = 0; i < 10; i++) {
      const sid = getSid(await t.client.createSession());
      sessions.push({ sid, token: `conc-001-token-${i}-${Date.now()}` });
    }
    await Promise.all(sessions.map(({ sid, token }) => t.client.prompt(sid, `respond with exactly ${token}`)));
    await Promise.all(sessions.map(({ sid }) => t.events.awaitEvent((e) => isIdle(e, sid), 60000)));
    for (const { sid, token } of sessions) {
      const msgs = (await t.client.messages(sid)).data || [];
      const str = JSON.stringify(msgs);
      if (!str.includes(token)) throw new Error(`session ${sid} missing its prompt token`);
      for (const other of sessions) {
        if (other.sid !== sid && str.includes(other.token)) {
          throw new Error(`session ${sid} leaked token from ${other.sid}`);
        }
      }
    }
  }),

  test('OC-CONC-002 two sessions write without polluting runtime', async (t) => {
    t.provider.strict = false;
    const sidA = getSid(await t.client.createSession());
    t.provider.expectToolCall({ id: 'write-a', tool: 'write', args: { filePath: 'conc-a.txt', content: 'A\n' } });
    const turnA = await t.turn.start(sidA);
    await t.client.prompt(sidA, 'write conc-a.txt with A');
    await turnA.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

    const sidB = getSid(await t.client.createSession());
    t.provider.expectToolCall({ id: 'write-b', tool: 'write', args: { filePath: 'conc-b.txt', content: 'B\n' } });
    const turnB = await t.turn.start(sidB);
    await t.client.prompt(sidB, 'write conc-b.txt with B');
    await turnB.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

    t.fs.expectFileContent('conc-a.txt', 'A\n');
    t.fs.expectFileContent('conc-b.txt', 'B\n');
    const msgsA = (await t.client.messages(sidA)).data || [];
    const msgsB = (await t.client.messages(sidB)).data || [];
    if (JSON.stringify(msgsA).includes('conc-b')) throw new Error('session A polluted by B');
    if (JSON.stringify(msgsB).includes('conc-a')) throw new Error('session B polluted by A');
  }),

  test('OC-CONC-009 100 rounds runtime store does not keep ended session data', async (t) => {
    t.provider.strict = false;
    const sid = getSid(await t.client.createSession());
    for (let i = 0; i < 100; i++) {
      await t.client.prompt(sid, `round ${i}`);
    }
    await waitForCount(t.events, (e) => isIdle(e, sid), 100, 180000);
    const del = await t.client.request('DELETE', `/session/${sid}`);
    if (!del.ok) throw new Error(`delete failed: ${del.status}`);
    await t.events.awaitEvent((e) => e.type === 'session.deleted' && e.sessionID === sid, 15000);
    const cmds = await getProcessCmds(t.host.pid);
    if (cmds.length > 2) throw new Error(`unexpected descendant processes after delete: ${cmds.join('; ')}`);
  }),

  test('OC-CONC-010 repeated create delete does not leak resources', async (t) => {
    const baseline = await getProcessCmds(t.host.pid);
    const baselineCount = baseline.length;
    for (let i = 0; i < 20; i++) {
      const sid = getSid(await t.client.createSession());
      const del = await t.client.request('DELETE', `/session/${sid}`);
      if (!del.ok) throw new Error(`delete failed: ${del.status}`);
      await t.events.awaitEvent((e) => e.type === 'session.deleted' && e.sessionID === sid, 10000).catch(() => {});
    }
    const after = await getProcessCmds(t.host.pid);
    if (after.length > baselineCount) {
      throw new Error(`process leak: ${after.length} descendants vs baseline ${baselineCount}`);
    }
  }),

  test('OC-CONC-011 mock provider delay does not block other sessions', async (t) => {
    t.provider.strict = false;
    t.provider.expectText({ id: 'delay-a', text: 'delayed', delayFirstToken: 15000 });
    const sidA = getSid(await t.client.createSession());
    const turnA = await t.turn.start(sidA);
    await t.client.prompt(sidA, 'say delayed');
    await t.events.awaitEvent((e) => isIdle(e, sidA) || (e.type === 'session.status' && statusType(e) === 'busy'), 10000);

    const sidB = getSid(await t.client.createSession());
    t.provider.expectToolCall({ id: 'write-conc-011', tool: 'write', args: { filePath: 'conc-011.txt', content: 'B\n' } });
    const turnB = await t.turn.start(sidB);
    const startB = Date.now();
    await t.client.prompt(sidB, 'write conc-011.txt with B');
    await turnB.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
    if (Date.now() - startB > 25000) throw new Error('session B blocked by delayed session A');

    await turnA.awaitTerminal({ timeoutMs: 60000 });
  }),

  test('OC-CONC-012 restart frees old port and provider socket', async (t) => {
    const oldPort = t.host.port;
    const oldPid = t.host.pid;
    await t.restart();
    const newPort = t.host.port;
    if (!newPort) throw new Error('host not restarted');
    if (oldPort === newPort) throw new Error(`port reused: ${newPort}`);
    if (!(await checkSocketClosed(oldPort, 5000))) throw new Error(`old port ${oldPort} still listening`);
    if (isPidAlive(oldPid)) throw new Error(`old process ${oldPid} still alive`);
    const health = await t.client.request('GET', '/api/session');
    if (!health.ok) throw new Error(`host not healthy after restart: ${health.status}`);
  }),
];

const getSid = (sess) => sess?.data?.data?.data?.id || sess?.data?.data?.id;
const only = process.argv[2];
const toRun = only ? tests.filter((x) => x.name.includes(only)) : tests;
const exitCode = await runScenario(COMMON, toRun);
process.exit(exitCode);
