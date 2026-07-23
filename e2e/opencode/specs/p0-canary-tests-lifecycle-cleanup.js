/**
 * p0-canary-tests-lifecycle-cleanup.js — Session delete cleanup and isolation.
 * Kept under the 250-line Kolmogorov line budget.
 */

import { runScenario, getSessionId } from '../../../testkit/opencode/scenario.js';
import { TIMEOUTS, findToolPart } from './p0-canary-utils.js';
import { isPidAlive, getDescendantPids } from '../../../testkit/opencode/process-host-checks.js';

const COMMON = { plugin: true, timeoutMs: 120000, contextLimit: 20000, allowSynthetic: true, allowTitleGen: true };

async function getProcessCmds(pid) {
  const pids = await getDescendantPids(pid);
  if (pids.length === 0) return [];
  const { execSync } = await import('node:child_process');
  const out = execSync(`ps -p ${pids.join(',')} -o cmd= 2>/dev/null || true`).toString().trim();
  return out.split(/\r?\n/).map((s) => s.trim()).filter(Boolean);
}

function findPtyId(messages) {
  for (const m of messages || []) {
    for (const p of m.parts || []) {
      if (p.type === 'tool' && p.tool === 'pty_spawn') {
        const match = String(p.state?.output || '').match(/id: (pty_[a-zA-Z0-9]+)/);
        if (match) return match[1];
      }
    }
  }
  return null;
}

function test(name, fn) { return { name, fn }; }

const tests = [
  test('OC-LIFE-011 session deleted cleans PTY and executor children', async (t) => {
    const sid = getSid(await t.client.createSession());
    t.provider.strict = false;
    t.provider.expectToolCall({
      id: 'pty-spawn-011',
      tool: 'pty_spawn',
      args: { command: 'sleep', args: ['60'], description: 'life 011' },
    });
    const turn = await t.turn.start(sid);
    await t.client.prompt(sid, 'spawn a PTY with sleep 60');
    await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

    const messages = (await t.client.messages(sid)).data || [];
    const ptyId = findPtyId(messages);
    if (!ptyId) throw new Error('pty id not found');
    const before = await getProcessCmds(t.host.pid);
    if (!before.some((c) => c.includes('sleep 60'))) throw new Error('PTY child not running before delete');

    const del = await t.client.request('DELETE', `/session/${sid}`);
    if (!del.ok) throw new Error(`delete failed: ${del.status}`);

    await t.events.awaitEvent((e) => e.type === 'session.deleted' && e.sessionID === sid, 15000);

    const deadline = Date.now() + 10000;
    let cmds = before;
    while (Date.now() < deadline) {
      cmds = await getProcessCmds(t.host.pid);
      if (!cmds.some((c) => c.includes('sleep 60'))) break;
      await new Promise((r) => setImmediate(r));
    }
    if (cmds.some((c) => c.includes('sleep 60')) && isPidAlive(t.host.pid)) {
      throw new Error('PTY child still alive after session delete');
    }
  }),

  test('OC-LIFE-012 delete session A does not affect session B', async (t) => {
    const sidA = getSid(await t.client.createSession());
    t.provider.strict = false;
    t.provider.expectToolCall({ id: 'write-a', tool: 'write', args: { filePath: 'a.txt', content: 'A\n' } });
    const turnA = await t.turn.start(sidA);
    await t.client.prompt(sidA, 'write a.txt with A');
    await turnA.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

    const sidB = getSid(await t.client.createSession());
    t.provider.expectToolCall({ id: 'write-b', tool: 'write', args: { filePath: 'b.txt', content: 'B\n' } });
    const turnB = await t.turn.start(sidB);
    await t.client.prompt(sidB, 'write b.txt with B');

    const delA = await t.client.request('DELETE', `/session/${sidA}`);
    if (!delA.ok) throw new Error(`delete A failed: ${delA.status}`);
    await t.events.awaitEvent((e) => e.type === 'session.deleted' && e.sessionID === sidA, 15000);

    await turnB.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

    t.fs.expectFileContent('a.txt', 'A\n');
    t.fs.expectFileContent('b.txt', 'B\n');
    const msgs = (await t.client.messages(sidB)).data || [];
    const msgsStr = JSON.stringify(msgs);
    if (msgsStr.includes('a.txt') || msgsStr.includes('life A')) {
      throw new Error('session B messages leaked content from session A');
    }
  }),
];

const getSid = (sess) => sess?.data?.data?.data?.id || sess?.data?.data?.id;
const only = process.argv[2];
const toRun = only ? tests.filter((x) => x.name.includes(only)) : tests;
const exitCode = await runScenario(COMMON, toRun);
process.exit(exitCode);
