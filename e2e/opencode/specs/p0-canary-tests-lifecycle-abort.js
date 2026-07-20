/**
 * p0-canary-tests-lifecycle-abort.js — Abort behavior and pending tool execution.
 * Kept under the 250-line Kolmogorov line budget.
 */

import { runScenario, getSessionId } from '../harness/scenario.js';
import { TIMEOUTS } from './p0-canary-utils.js';
import { waitForCondition } from './p0-canary-ndjson.js';
import { getDescendantPids } from '../harness/process-host-checks.js';
import fs from 'node:fs';

const COMMON = { plugin: true, timeoutMs: 120000, contextLimit: 20000, allowSynthetic: true, allowTitleGen: true };
const statusType = (e) => e.properties?.status?.type || e.properties?.status;
const isIdle = (e, sid) => e.sessionID === sid && (e.type === 'session.idle' || (e.type === 'session.status' && statusType(e) === 'idle'));
const isError = (e, sid) => e.sessionID === sid && e.type === 'session.error';
const toolCall = (e, sid) => e.sessionID === sid && e.type === 'message.updated' && e.finishReason === 'tool-calls';

function test(name, fn) { return { name, fn }; }
async function getProcessCmds(pid) {
  const pids = await getDescendantPids(pid);
  if (pids.length === 0) return [];
  const { execSync } = await import('node:child_process');
  const out = execSync(`ps -p ${pids.join(',')} -o cmd= 2>/dev/null || true`).toString().trim();
  return out.split(/\r?\n/).map((s) => s.trim()).filter(Boolean);
}

const tests = [
  test('OC-LIFE-006 abort produces MessageAbortedError or terminal idle', async (t) => {
    const sid = getSid(await t.client.createSession());
    t.provider.strict = false;
    t.provider.expectToolCall({
      id: 'exec-abort',
      tool: 'executor',
      args: { language: 'shell', command: 'sleep 30', mode: 'rw', what_to_summarize: 'abort test', max_bytes: 100 },
    });
    const turn = await t.turn.start(sid);
    await t.client.prompt(sid, 'run sleep 60');
    await t.events.awaitEvent((e) => toolCall(e, sid), 15000);
    await t.client.abort(sid);
    const terminal = await t.events.awaitEvent((e) => isIdle(e, sid) || isError(e, sid), 15000);
    if (terminal.type === 'session.error') {
      const ok = ['MessageAbortedError', 'AbortError', 'AbortError'];
      if (!ok.includes(terminal.errorName)) {
        throw new Error(`unexpected abort error name: ${terminal.errorName}`);
      }
    }
  }),

  test('OC-LIFE-007 abort terminates pending executor child process', async (t) => {
    const sid = getSid(await t.client.createSession());
    t.provider.strict = false;
    const marker = 'life-007.marker';
    const cmd = `touch ${marker} && sleep 30`;
    t.provider.expectToolCall({
      id: 'exec-abort-child',
      tool: 'executor',
      args: { language: 'shell', command: cmd, mode: 'rw', what_to_summarize: 'abort child test', max_bytes: 100 },
    });
    const turn = await t.turn.start(sid);
    await t.client.prompt(sid, `run ${cmd}`);
    await waitForCondition(() => fs.existsSync(`${t.host.workDir}/${marker}`), (x) => x, 15000);

    const before = await getProcessCmds(t.host.pid);
    if (!before.some((c) => c.includes('sleep 30'))) throw new Error('executor child not running before abort');

    await t.client.abort(sid);
    await t.events.awaitEvent((e) => isIdle(e, sid) || isError(e, sid), 15000);

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
  }),

  test('OC-LIFE-008 tool input already issued is not erased by abort', async (t) => {
    const sid = getSid(await t.client.createSession());
    t.provider.strict = false;
    t.provider.expectToolCall({
      id: 'exec-abort-keep',
      tool: 'executor',
      args: { language: 'shell', command: 'echo life-008 && sleep 30', mode: 'rw', what_to_summarize: 'keep test', max_bytes: 100 },
    });
    const turn = await t.turn.start(sid);
    await t.client.prompt(sid, 'run echo life-008 and sleep 30');
    await t.events.awaitEvent((e) => toolCall(e, sid), 15000);
    await t.client.abort(sid);
    await t.events.awaitEvent((e) => isIdle(e, sid) || isError(e, sid), 15000);
    const messages = (await t.client.messages(sid)).data || [];
    const parts = messages.flatMap((m) => m.parts || []);
    const toolPart = parts.find((p) => p.type === 'tool' && p.tool === 'executor');
    if (!toolPart) throw new Error('executor tool part missing from messages after abort');
    if (!JSON.stringify(toolPart.state?.input || {}).includes('life-008')) {
      throw new Error('executor tool input erased by abort');
    }
  }),
];

const getSid = (sess) => sess?.data?.data?.data?.id || sess?.data?.data?.id;
const only = process.argv[2];
const toRun = only ? tests.filter((x) => x.name.includes(only)) : tests;
const exitCode = await runScenario(COMMON, toRun);
process.exit(exitCode);
