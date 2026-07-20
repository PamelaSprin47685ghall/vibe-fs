/**
 * p0-canary-tests-fallback-isolation.js — Fallback isolation / lifecycle P0 canary tests.
 * Imported by p0-canary-tests-fallback.js so the runner stays unchanged.
 */

import { getSessionId } from '../harness/scenario.js';
import { waitForCondition, waitForNdjson } from './p0-canary-ndjson.js';
import { TIMEOUTS } from './p0-canary-utils.js';

function expectNoSessionError(t, sid) {
  t.events.expectCount({ type: 'session.error', sessionID: sid, count: 0 });
}

function continuationEventsOf(events, sid) {
  return events.filter((e) => e.Session === sid && e.Kind?.startsWith('continuation_'));
}

function humanTurnEventsOf(events, sid) {
  return events.filter((e) => e.Session === sid && e.Kind === 'human_turn_started');
}

const tests = [
  {
    name: 'OC-FB-023 two sessions fallback leases isolated',
    fn: async (t) => {
      const sess1 = await t.client.createSession();
      const sess2 = await t.client.createSession();
      const sid1 = getSessionId(sess1);
      const sid2 = getSessionId(sess2);

      t.provider.expectText({ id: 'fb-empty-a', text: '' });
      t.provider.expectText({ id: 'fb-empty-b', text: '' });

      const turn1 = await t.turn.start(sid1);
      const turn2 = await t.turn.start(sid2);

      await t.client.prompt(sid1, 'empty a');
      await t.client.prompt(sid2, 'empty b');

      await waitForCondition(
        () => t.provider.syntheticRequests.filter((r) => r.marker === 'zwsp'),
        (requests) => requests.length === 2,
        TIMEOUTS.quick,
      );

      await Promise.all([
        turn1.awaitTerminal({ timeoutMs: TIMEOUTS.quick }),
        turn2.awaitTerminal({ timeoutMs: TIMEOUTS.quick }),
      ]);

      const ndjson = await waitForNdjson(
        t.host.workDir,
        (events) =>
          events.some((e) => e.Session === sid1 && e.Kind === 'continuation_host_accepted')
          && events.some((e) => e.Session === sid2 && e.Kind === 'continuation_host_accepted'),
        TIMEOUTS.quick,
      );

      const c1 = continuationEventsOf(ndjson, sid1);
      const c2 = continuationEventsOf(ndjson, sid2);
      const h1 = humanTurnEventsOf(ndjson, sid1);
      const h2 = humanTurnEventsOf(ndjson, sid2);

      const req1 = c1.find((e) => e.Kind === 'continuation_requested')?.Payload || {};
      const req2 = c2.find((e) => e.Kind === 'continuation_requested')?.Payload || {};
      const ht1 = h1[0]?.Payload || {};
      const ht2 = h2[0]?.Payload || {};

      if (!req1.continuationId || !req2.continuationId) {
        throw new Error('Missing continuationId for one or both sessions');
      }
      if (req1.continuationId === req2.continuationId) {
        throw new Error('Two sessions share the same continuationId');
      }
      if (req1.humanTurnId === req2.humanTurnId) {
        throw new Error('Two sessions share the same humanTurnId');
      }
      if (req1.continuationOrdinal !== '1' || req2.continuationOrdinal !== '1') {
        throw new Error(`Unexpected continuationOrdinal: ${req1.continuationOrdinal}, ${req2.continuationOrdinal}`);
      }
      if (ht1.humanTurnOrdinal !== '1' || ht2.humanTurnOrdinal !== '1') {
        throw new Error(`Unexpected humanTurnOrdinal: ${ht1.humanTurnOrdinal}, ${ht2.humanTurnOrdinal}`);
      }
      if (req1.humanTurnId !== ht1.turnId || req2.humanTurnId !== ht2.turnId) {
        throw new Error('Continuation humanTurnId does not match session human_turn_started turnId');
      }
      if (req1.agent !== req2.agent || req1.model !== req2.model) {
        throw new Error(`Agent/model differ across isolated sessions: ${req1.agent}/${req1.model} vs ${req2.agent}/${req2.model}`);
      }

      expectNoSessionError(t, sid1);
      expectNoSessionError(t, sid2);
    },
  },
];

export default tests;
