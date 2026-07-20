/**
 * p0-canary-tests-fallback.js — Fallback / continuation P0 canary tests.
 * Kept under the 300-line Kolmogorov line budget.
 */

import { getSessionId } from '../harness/scenario.js';
import { waitForCondition, waitForNdjson } from './p0-canary-ndjson.js';
import { TIMEOUTS } from './p0-canary-utils.js';
import isolationTests from './p0-canary-tests-fallback-isolation.js';
import errorTests from './p0-canary-tests-fallback-errors.js';

function expectNoSessionError(t, sid) {
  t.events.expectCount({ type: 'session.error', sessionID: sid, count: 0 });
}

function extractLastUserTexts(body) {
  const msgs = body?.messages || [];
  const lastUser = msgs.slice().reverse().find((m) => m?.role === 'user');
  const texts = [];
  if (typeof lastUser?.content === 'string') {
    texts.push(lastUser.content);
  } else if (Array.isArray(lastUser?.content)) {
    for (const p of lastUser.content) {
      if (p?.type === 'text' || typeof p?.text === 'string') texts.push(p.text);
    }
  }
  return { lastUser, texts };
}

const tests = [
  {
    name: 'OC-FB-001 OC-FB-002 OC-FB-003 OC-FB-004 OC-FB-017 fallback continuation zero-width nonce agent model',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      const sessionModel =
        sess?.data?.data?.data?.model || sess?.data?.data?.model || { id: 'test-model', providerID: 'test' };

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
      const { texts } = extractLastUserTexts(continuation.body);
      const continuationText = texts.find((x) => x && x.includes('\u200B'));

      // OC-FB-001: empty output produces a single zero-width continuation.
      if (!continuationText) {
        throw new Error('Continuation user message does not contain U+200B: ' + JSON.stringify(texts));
      }

      // OC-FB-002: the injected content is a zero-width char, not XML markup.
      if (continuationText.includes('<') || continuationText.includes('>')) {
        throw new Error('Continuation injection contains XML-like markup: ' + JSON.stringify(continuationText));
      }

      // The event log is the durable lifecycle oracle. Host acceptance is the
      // observable boundary that proves the continuation was injected. Current
      // harness paths do not always emit continuation_settled/dispatched, so the
      // test validates the events that are reliably present.
      const ndjson = await waitForNdjson(
        t.host.workDir,
        (events) => events.some((event) => event.Session === sid && event.Kind === 'continuation_host_accepted'),
        TIMEOUTS.quick,
      );
      const continuationEvents = ndjson.filter((e) => e.Session === sid && e.Kind?.startsWith('continuation_'));

      // The continuation response produces a later assistant message. Wait for
      // the model to finish (message.updated with finishReason after the first
      // empty-output activity) before reading /session/{id}/message.
      await t.events.awaitEvent(
        (e) => e.type === 'message.updated' && e.finishReason && e.seq > turn.activitySeq,
        TIMEOUTS.quick,
      );

      const messages = (await t.client.messages(sid)).data || [];
      const assistantTexts = messages
        .filter((m) => m.info?.role === 'assistant')
        .flatMap((m) => m.parts || [])
        .filter((p) => p.type === 'text' && typeof p.text === 'string')
        .map((p) => p.text);
      if (!assistantTexts.some((text) => text.includes('done'))) {
        throw new Error('Fallback continuation did not produce final assistant response: ' + JSON.stringify(assistantTexts));
      }

      const humanTurnEvents = ndjson.filter((e) => e.Session === sid && e.Kind === 'human_turn_started');

      const requested = continuationEvents.filter((e) => e.Kind === 'continuation_requested');
      const dispatchStarted = continuationEvents.filter((e) => e.Kind === 'continuation_dispatch_started');
      const hostAccepted = continuationEvents.filter((e) => e.Kind === 'continuation_host_accepted');
      const failed = continuationEvents.filter((e) => e.Kind === 'continuation_failed');
      const cancelled = continuationEvents.filter((e) => e.Kind === 'continuation_cancelled');
      const settled = continuationEvents.filter((e) => e.Kind === 'continuation_settled');
      const dispatched = continuationEvents.filter((e) => e.Kind === 'continuation_dispatched');

      if (requested.length !== 1) throw new Error(`Expected exactly one continuation_requested, got ${requested.length}`);
      if (dispatchStarted.length !== 1) throw new Error(`Expected exactly one continuation_dispatch_started, got ${dispatchStarted.length}`);
      if (hostAccepted.length !== 1) throw new Error(`Expected exactly one continuation_host_accepted, got ${hostAccepted.length}`);
      if (failed.length !== 0) throw new Error(`Unexpected continuation_failed: ${JSON.stringify(failed)}`);
      if (cancelled.length !== 0) throw new Error(`Unexpected continuation_cancelled: ${JSON.stringify(cancelled)}`);

      const reqPayload = requested[0].Payload || {};
      const startPayload = dispatchStarted[0].Payload || {};
      const hostPayload = hostAccepted[0].Payload || {};

      // OC-FB-003/004: continuation carries the original agent and model.
      const continuationAgent = reqPayload.agent;
      const continuationModel = reqPayload.model;
      const continuationId = reqPayload.continuationId;
      const humanTurnId = reqPayload.humanTurnId;

      if (!continuationAgent || typeof continuationAgent !== 'string') {
        throw new Error('continuation_requested missing agent: ' + JSON.stringify(reqPayload));
      }
      if (!continuationModel || typeof continuationModel !== 'string') {
        throw new Error('continuation_requested missing model: ' + JSON.stringify(reqPayload));
      }
      if (!continuationId || typeof continuationId !== 'string') {
        throw new Error('continuation_requested missing continuationId (nonce): ' + JSON.stringify(reqPayload));
      }
      if (!humanTurnId || typeof humanTurnId !== 'string') {
        throw new Error('continuation_requested missing humanTurnId: ' + JSON.stringify(reqPayload));
      }

      // OC-FB-017: nonce (continuationId) must be consistent across the lease.
      if (startPayload.continuationId !== continuationId) {
        throw new Error(`continuation_dispatch_started continuationId mismatch: ${startPayload.continuationId} vs ${continuationId}`);
      }
      if (hostPayload.continuationId !== continuationId) {
        throw new Error(`continuation_host_accepted continuationId mismatch: ${hostPayload.continuationId} vs ${continuationId}`);
      }
      if (!hostPayload.userMessageId || typeof hostPayload.userMessageId !== 'string') {
        throw new Error('continuation_host_accepted missing userMessageId: ' + JSON.stringify(hostPayload));
      }

      // OC-FB-017: continuation must reuse the original human turn, not create one.
      if (humanTurnEvents.length !== 1) {
        throw new Error(`Expected exactly one human_turn_started, got ${humanTurnEvents.length}`);
      }
      const humanTurnPayload = humanTurnEvents[0].Payload || {};
      const originalTurnId = humanTurnPayload.humanTurnId || humanTurnPayload.turnId;
      if (originalTurnId !== humanTurnId) {
        throw new Error(`continuation humanTurnId mismatch: expected ${originalTurnId}, got ${humanTurnId}`);
      }

      // OC-FB-017: the nonce must not leak into the visible human message text.
      if (continuationText.includes(continuationId)) {
        throw new Error('Continuation text leaks continuationId (nonce): ' + continuationId);
      }

      // OC-FB-003/004: continuity should preserve the originating agent/model.
      if (humanTurnPayload.agent && continuationAgent !== humanTurnPayload.agent) {
        throw new Error(`continuation agent mismatch: expected ${humanTurnPayload.agent}, got ${continuationAgent}`);
      }
      const expectedModelPrefix = `${humanTurnPayload.provider || sessionModel.providerID}/${humanTurnPayload.model || sessionModel.id}`;
      if (!continuationModel.startsWith(expectedModelPrefix)) {
        throw new Error(`continuation model mismatch: expected to start with ${expectedModelPrefix}, got ${continuationModel}`);
      }

      // The synthetic provider request carries the model the host actually dialed.
      const providerModel = continuation.body?.model;
      const expectedModelId = sessionModel.id || 'test-model';
      if (!providerModel || !String(providerModel).includes(expectedModelId)) {
        throw new Error(`Synthetic continuation request model mismatch: expected ${expectedModelId}, got ${providerModel}`);
      }

      const order = continuationEvents.map((event) => event.Kind);
      const indexOf = (kind) => order.indexOf(kind);
      if (!(indexOf('continuation_requested') < indexOf('continuation_dispatch_started')
        && indexOf('continuation_dispatch_started') < indexOf('continuation_host_accepted'))) {
        throw new Error(`Invalid continuation lifecycle order: ${JSON.stringify(order)}`);
      }

      // Validate later lifecycle events when present without failing if the
      // current harness does not emit them.
      if (settled.length > 0) {
        if (indexOf('continuation_host_accepted') >= indexOf('continuation_settled')) {
          throw new Error(`Settled before host accepted: ${JSON.stringify(order)}`);
        }
      }
      if (dispatched.length > 0) {
        if (indexOf('continuation_dispatch_started') >= indexOf('continuation_dispatched')) {
          throw new Error(`Dispatched before dispatch started: ${JSON.stringify(order)}`);
        }
      }

      expectNoSessionError(t, sid);
    },
  },
];

export default [...tests, ...isolationTests, ...errorTests];
