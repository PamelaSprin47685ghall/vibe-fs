/**
 * p0-canary-tests-fallback.js — Fallback / continuation P0 canary tests.
 * Kept under the 300-line Kolmogorov line budget.
 */

import { getSessionId } from '../harness/scenario.js';
import { waitForCondition, waitForNdjson } from './p0-canary-ndjson.js';
import { TIMEOUTS } from './p0-canary-utils.js';

function expectNoSessionError(t, sid) {
  t.events.expectCount({ type: 'session.error', sessionID: sid, count: 0 });
}

const tests = [
  {
    name: 'OC-FB-001 empty output injects zero-width continuation',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectText({ id: 'fb-empty-output', text: '' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'say nothing');

      // The fallback continuation is dispatched as a synthetic prompt containing
      // U+200B. Wait for it rather than relying solely on turn terminal events,
      // because the first idle (empty output) and the continuation idle are
      // separate events and the harness may observe them out of order.
      await waitForCondition(
        () => t.provider.syntheticRequests.filter((request) => request.marker === 'zwsp'),
        (requests) => requests.length === 1,
        TIMEOUTS.quick,
      );
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.quick });

      const zwsp = t.provider.syntheticRequests.filter((r) => r.marker === 'zwsp');
      if (zwsp.length === 0) throw new Error('Zero-width continuation not dispatched');
      if (zwsp.length > 1) throw new Error(`Expected one zero-width continuation, got ${zwsp.length}`);

      const continuation = zwsp[0];
      const msgs = continuation.body?.messages || [];
      const lastUser = msgs.slice().reverse().find((m) => m?.role === 'user');
      const texts = [];
      if (typeof lastUser?.content === 'string') {
        texts.push(lastUser.content);
      } else if (Array.isArray(lastUser?.content)) {
        for (const p of lastUser.content) {
          if (p?.type === 'text' || typeof p?.text === 'string') texts.push(p.text);
        }
      }
      if (!texts.some((x) => x && x.includes('\u200B'))) {
        throw new Error('Continuation user message does not contain U+200B: ' + JSON.stringify(texts));
      }

      const messages = (await t.client.messages(sid)).data || [];
      const assistantTexts = messages
        .filter((m) => m.info?.role === 'assistant')
        .flatMap((m) => m.parts || [])
        .filter((p) => p.type === 'text' && typeof p.text === 'string')
        .map((p) => p.text);
      if (!assistantTexts.some((text) => text.includes('done'))) {
        throw new Error('Fallback continuation did not produce final assistant response: ' + JSON.stringify(assistantTexts));
      }

      // The event log is the durable lifecycle oracle. Await settlement rather
      // than assuming the host idle event means the append queue has drained.
      const ndjson = await waitForNdjson(
        t.host.workDir,
        (events) => events.some((event) => event.Session === sid && event.Kind === 'continuation_settled'),
        TIMEOUTS.quick,
      );
      const continuationEvents = ndjson.filter((e) => e.Session === sid && e.Kind?.startsWith('continuation_'));
      const requested = continuationEvents.filter((e) => e.Kind === 'continuation_requested');
      const dispatchStarted = continuationEvents.filter((e) => e.Kind === 'continuation_dispatch_started');
      const hostAccepted = continuationEvents.filter((e) => e.Kind === 'continuation_host_accepted');
      const settled = continuationEvents.filter((e) => e.Kind === 'continuation_settled');
      const failed = continuationEvents.filter((e) => e.Kind === 'continuation_failed');
      const cancelled = continuationEvents.filter((e) => e.Kind === 'continuation_cancelled');
      if (requested.length !== 1) throw new Error(`Expected exactly one continuation_requested, got ${requested.length}`);
      if (dispatchStarted.length !== 1) throw new Error(`Expected exactly one continuation_dispatch_started, got ${dispatchStarted.length}`);
      if (hostAccepted.length !== 1) throw new Error(`Expected exactly one continuation_host_accepted, got ${hostAccepted.length}`);
      if (settled.length !== 1) throw new Error(`Expected exactly one continuation_settled, got ${settled.length}: ${JSON.stringify(continuationEvents.map((e) => e.Kind))}`);
      if (failed.length !== 0) throw new Error(`Unexpected continuation_failed: ${JSON.stringify(failed)}`);
      if (cancelled.length !== 0) throw new Error(`Unexpected continuation_cancelled: ${JSON.stringify(cancelled)}`);
      const dispatched = continuationEvents.filter((e) => e.Kind === 'continuation_dispatched');
      if (dispatched.length !== 1) throw new Error(`Expected exactly one continuation_dispatched, got ${dispatched.length}`);

      const order = continuationEvents.map((event) => event.Kind);
      const indexOf = (kind) => order.indexOf(kind);
      if (!(indexOf('continuation_requested') < indexOf('continuation_dispatch_started')
        && indexOf('continuation_dispatch_started') < indexOf('continuation_host_accepted')
        && indexOf('continuation_host_accepted') < indexOf('continuation_settled'))) {
        throw new Error(`Invalid continuation lifecycle order: ${JSON.stringify(order)}`);
      }

      expectNoSessionError(t, sid);
    },
  },
];

export default tests;
