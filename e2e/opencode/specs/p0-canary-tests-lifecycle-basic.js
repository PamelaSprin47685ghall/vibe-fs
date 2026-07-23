/**
 * p0-canary-tests-lifecycle-basic.js — Lifecycle busy/idle/error/duplicate idle/hook async
 * and immediate-follow-up prompt tests.
 * Kept under the 250-line Kolmogorov line budget.
 */

import { runScenario, getSessionId } from '../../../testkit/opencode/scenario.js';
import { TIMEOUTS } from './p0-canary-utils.js';
import { waitForNdjson } from './p0-canary-ndjson.js';

const getSid = (sess) => sess?.data?.data?.data?.id || sess?.data?.data?.id;
const COMMON = { plugin: true, timeoutMs: 120000, contextLimit: 20000, allowSynthetic: true, allowTitleGen: true };

const statusType = (e) => e.properties?.status?.type || e.properties?.status;
const isBusy = (e, sid) => e.sessionID === sid && e.type === 'session.status' && statusType(e) === 'busy';
const isIdle = (e, sid) => e.sessionID === sid && (e.type === 'session.idle' || (e.type === 'session.status' && statusType(e) === 'idle'));
const isError = (e, sid) => e.sessionID === sid && e.type === 'session.error';

function test(name, fn) { return { name, fn }; }

const tests = [
  test('OC-LIFE-004 normal round transitions from busy to terminal idle', async (t) => {
    const sid = getSid(await t.client.createSession());
    t.provider.strict = false;
    t.provider.expectToolCall({ id: 'write-004', tool: 'write', args: { filePath: 'life-004.txt', content: 'ok\n' } });
    const turn = await t.turn.start(sid);
    await t.client.prompt(sid, 'write life-004.txt with ok');
    await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
    const startSeq = turn.eventSeqBefore;
    await t.events.awaitSequence([
      (e) => isBusy(e, sid) && e.seq > startSeq,
      (e) => isIdle(e, sid) && e.seq > startSeq,
    ], TIMEOUTS.prompt);
    t.events.expectCount({ type: 'session.idle', sessionID: sid, count: 1 });
  }),

  test('OC-LIFE-005 API error emits session.error then idle', async (t) => {
    const sid = getSid(await t.client.createSession());
    t.provider.strict = false;
    t.provider.expectText({ id: 'ctx-overflow', contextOverflow: true });
    const turn = await t.turn.start(sid);
    await t.client.prompt(sid, 'overflow the context');
    const err = await t.events.awaitEvent((e) => isError(e, sid), TIMEOUTS.prompt);
    if (err.errorName !== 'ContextOverflowError') {
      throw new Error(`expected ContextOverflowError, got ${err.errorName}`);
    }
    await t.events.awaitEvent((e) => isIdle(e, sid) && e.seq > err.seq, TIMEOUTS.prompt);
  }),

  test('OC-LIFE-010 repeated idle is idempotent per turn', async (t) => {
    const sid = getSid(await t.client.createSession());
    t.provider.strict = false;
    for (let i = 0; i < 2; i++) {
      t.provider.expectToolCall({ id: `write-010-${i}`, tool: 'write', args: { filePath: `life-010-${i}.txt`, content: `ok-${i}\n` } });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, `write life-010-${i}.txt with ok-${i}`);
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
    }
    const statusIdle = t.events.allEvents.filter((e) => e.sessionID === sid && e.type === 'session.status' && statusType(e) === 'idle').length;
    const sessionIdle = t.events.allEvents.filter((e) => e.sessionID === sid && e.type === 'session.idle').length;
    if (statusIdle < 2) throw new Error(`expected at least 2 session.status idle, got ${statusIdle}`);
    if (sessionIdle !== 1) throw new Error(`session.idle should be idempotent (1), got ${sessionIdle}`);
  }),

  test('OC-LIFE-013 event hook async work is observable without fixed sleep', async (t) => {
    const sid = getSid(await t.client.createSession());
    t.provider.strict = false;
    t.provider.expectToolCall({ id: 'write-013', tool: 'write', args: { filePath: 'life-013.txt', content: 'ok\n' } });
    const turn = await t.turn.start(sid);
    await t.client.prompt(sid, 'write life-013.txt');
    await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
    await waitForNdjson(
      t.host.workDir,
      (events) => events.some((e) => e.Session === sid && !!e.Kind),
      TIMEOUTS.quick,
    );
  }),

  test('OC-LIFE-015 next prompt immediately after idle is race-free', async (t) => {
    const sid = getSid(await t.client.createSession());
    t.provider.strict = false;
    t.provider.expectToolCall({ id: 'write-015a', tool: 'write', args: { filePath: 'life-015a.txt', content: 'a\n' } });
    const turn1 = await t.turn.start(sid);
    await t.client.prompt(sid, 'write life-015a.txt');
    await turn1.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
    t.provider.expectToolCall({ id: 'write-015b', tool: 'write', args: { filePath: 'life-015b.txt', content: 'b\n' } });
    const turn2 = await t.turn.start(sid);
    await t.client.prompt(sid, 'write life-015b.txt');
    await turn2.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
    t.fs.expectFile('life-015a.txt');
    t.fs.expectFile('life-015b.txt');
  }),
];

const only = process.argv[2];
const toRun = only ? tests.filter((x) => x.name.includes(only)) : tests;
const exitCode = await runScenario(COMMON, toRun);
process.exit(exitCode);
