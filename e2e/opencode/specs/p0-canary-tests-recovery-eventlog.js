/**
 * p0-canary-tests-recovery-eventlog.js — NDJSON append and integrity tests.
 */

import { getSessionId } from '../../../testkit/opencode/scenario.js';
import { waitForLoopCommand, validateNdjson } from './p0-canary-tests-recovery-helpers.js';
import { TIMEOUTS } from './p0-canary-utils.js';
import fs from 'node:fs';

function getHasPlugin(t) {
  return t.host._startOpts.pluginPaths && t.host._startOpts.pluginPaths.length > 0;
}

const testNdjsonCreationIsolation = {
  name: 'OC-ES-001 OC-ES-002 OC-ES-012 NDJSON creation, structure validation, and session isolation',
  fn: async (t) => {
    const hasPlugin = getHasPlugin(t);
    const sidA = getSessionId(await t.client.createSession());
    if (hasPlugin) await waitForLoopCommand(t.client);

    t.provider.expectText({ id: 'warm-a', text: 'ok' });
    const turnA = await t.turn.start(sidA);
    await t.client.prompt(sidA, 'hello A');
    await turnA.awaitTerminal({ timeoutMs: TIMEOUTS.quick, requireAssistantTerminal: false });

    if (hasPlugin) {
      const cmdTurnA = await t.turn.start(sidA);
      const cmdRes = await t.client.runCommand(sidA, 'loop', 'task A', 10000);
      if (!cmdRes.ok) throw new Error('loop A failed');
      await cmdTurnA.awaitTerminal({ timeoutMs: TIMEOUTS.quick, requireAssistantTerminal: false });
    }

    const sidB = getSessionId(await t.client.createSession());
    t.provider.expectText({ id: 'warm-b', text: 'ok' });
    const turnB = await t.turn.start(sidB);
    await t.client.prompt(sidB, 'hello B');
    await turnB.awaitTerminal({ timeoutMs: TIMEOUTS.quick, requireAssistantTerminal: false });

    const events = validateNdjson(t.host.workDir, [sidA, sidB]);
    if (events.length === 0) throw new Error('No events written to NDJSON');

    if (hasPlugin) {
      const loopEvents = events.filter(e => e.Kind === 'loop_activated');
      if (loopEvents.length !== 1) {
        throw new Error(`Expected exactly 1 loop_activated event, got ${loopEvents.length}`);
      }
      if (loopEvents[0].Session !== sidA) {
        throw new Error(`loop_activated event associated with session ${loopEvents[0].Session}, expected ${sidA}`);
      }
      const bLoop = events.filter(e => e.Session === sidB && e.Kind === 'loop_activated');
      if (bLoop.length !== 0) throw new Error('Session B incorrectly has loop_activated events');
    }
  },
};

const testConcurrentAppends = {
  name: 'OC-ES-003 concurrent appends to NDJSON do not interleave or corrupt',
  fn: async (t) => {
    const sids = [];
    for (let i = 0; i < 4; i++) {
      sids.push(getSessionId(await t.client.createSession()));
      t.provider.expectText({ id: `concurrent-warm-${i}`, text: `ok-${i}` });
    }
    const turns = [];
    for (const sid of sids) turns.push(await t.turn.start(sid));
    await Promise.all(sids.map((sid, idx) => t.client.prompt(sid, `hello concurrent ${idx}`)));
    await Promise.all(turns.map(turn => turn.awaitTerminal({ timeoutMs: TIMEOUTS.quick, requireAssistantTerminal: false })));
    validateNdjson(t.host.workDir, sids);
  },
};

const testAppendFailureNoExceed = {
  name: 'OC-ES-013 append failure: memory does not exceed durable',
  fn: async (t) => {
    const sid = getSessionId(await t.client.createSession());
    t.provider.expectText({ id: 'warm', text: 'ok' });
    const turn = await t.turn.start(sid);
    await t.client.prompt(sid, 'hello');
    await turn.awaitTerminal({ timeoutMs: TIMEOUTS.quick, requireAssistantTerminal: false });

    const p = t.host.workDir + '/.wanxiangshu.ndjson';
    fs.chmodSync(p, 0o444);
    try {
      const turn2 = await t.turn.start(sid);
      await t.client.prompt(sid, 'hello2', null, 5000);
      await turn2.awaitTerminal({ timeoutMs: 5000, requireAssistantTerminal: false }).catch(() => {});
    } finally {
      fs.chmodSync(p, 0o644);
    }

    const msgs = await t.client.messages(sid);
    if (JSON.stringify(msgs.data).includes('hello2')) {
      throw new Error('Memory mutated despite append failure: "hello2" exists in history');
    }
  },
};

export default [testNdjsonCreationIsolation, testConcurrentAppends, testAppendFailureNoExceed];
