/**
 * p0-canary-tests-fallback-errors.js — Fallback error-class / duplicate-idle P0.
 * Real oracles: syntheticRequests count, NDJSON continuation_*, EventProbe errors.
 */

import { getSessionId } from '../harness/scenario.js';
import { waitForCondition, waitForNdjson } from './p0-canary-ndjson.js';
import { TIMEOUTS } from './p0-canary-utils.js';

function expectNoSessionError(t, sid) {
  t.events.expectCount({ type: 'session.error', sessionID: sid, count: 0 });
}

function zwspCount(t) {
  return t.provider.syntheticRequests.filter((r) => r.marker === 'zwsp').length;
}

function continuationKinds(events, sid) {
  return events.filter((e) => e.Session === sid && String(e.Kind || '').startsWith('continuation_'));
}

const tests = [
  {
    name: 'OC-FB-007 non-retryable provider error does not continue',
    fn: async (t) => {
      const sid = getSessionId(await t.client.createSession());
      t.provider.expectError({
        id: 'fb-007-non-retry',
        status: 400,
        body: {
          error: {
            name: 'APIError',
            message: 'invalid request',
            type: 'invalid_request_error',
            isRetryable: false,
          },
        },
      });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'trigger non-retryable');
      await turn.awaitTerminal({
        timeoutMs: TIMEOUTS.prompt,
        requireAssistantTerminal: false,
      });

      // Non-retryable must not inject zero-width continuation.
      if (zwspCount(t) !== 0) {
        throw new Error(`Expected 0 zwsp continuations after non-retryable error, got ${zwspCount(t)}`);
      }

      const ndjson = await waitForNdjson(
        t.host.workDir,
        (events) => events.some((e) => e.Session === sid),
        TIMEOUTS.quick,
      );
      const cont = continuationKinds(ndjson, sid);
      const requested = cont.filter((e) => e.Kind === 'continuation_requested');
      if (requested.length !== 0) {
        throw new Error(`non-retryable must not request continuation, got ${requested.length}: ${JSON.stringify(requested)}`);
      }

      // Session should reach a terminal idle/error without synthetic continue.
      const errEvents = t.events.bySession(sid).filter((e) => e.type === 'session.error');
      const idleEvents = t.events.bySession(sid).filter((e) => e.type === 'session.idle');
      if (errEvents.length === 0 && idleEvents.length === 0) {
        throw new Error('expected session.error or session.idle after non-retryable provider error');
      }
    },
  },

  {
    name: 'OC-FB-008 MessageAbortedError does not continue',
    fn: async (t) => {
      const sid = getSessionId(await t.client.createSession());
      // Long-running tool so we can abort mid-turn before any empty-output settle.
      t.provider.expectToolCall({
        id: 'fb-008-sleep',
        tool: 'executor',
        args: {
          language: 'shell',
          command: 'sleep 30',
          mode: 'rw',
          what_to_summarize: 'abort no continue',
          max_bytes: 100,
        },
      });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'run long sleep then abort');
      await t.events.awaitEvent(
        (e) =>
          e.sessionID === sid
          && e.type === 'message.updated'
          && /tool[-_]calls?/.test(String(e.finishReason || '')),
        TIMEOUTS.prompt,
      );
      await t.client.abort(sid);
      await t.events.awaitEvent(
        (e) =>
          e.sessionID === sid
          && (e.type === 'session.idle'
            || e.type === 'session.error'
            || (e.type === 'session.status' && (e.status === 'idle' || e.properties?.status?.type === 'idle'))),
        TIMEOUTS.prompt,
      );

      if (zwspCount(t) !== 0) {
        throw new Error(`abort must not dispatch zwsp continuation, got ${zwspCount(t)}`);
      }

      const ndjson = await waitForNdjson(
        t.host.workDir,
        (events) => events.some((e) => e.Session === sid),
        TIMEOUTS.quick,
      ).catch(() => []);
      const requested = continuationKinds(ndjson, sid).filter((e) => e.Kind === 'continuation_requested');
      if (requested.length !== 0) {
        throw new Error(`MessageAborted must not request continuation, got ${JSON.stringify(requested)}`);
      }

      // Optional: if host surfaces abort error name, it must be abort-class.
      const abortErr = t.events.bySession(sid).find((e) => e.type === 'session.error');
      if (abortErr) {
        const name = abortErr.errorName || abortErr.error?.name || '';
        const ok = ['MessageAbortedError', 'AbortError', ''].includes(name) || /abort/i.test(String(name));
        if (!ok) throw new Error(`unexpected error after abort: ${name}`);
      }
    },
  },

  {
    name: 'OC-FB-012 duplicate idle does not double-continue',
    fn: async (t) => {
      const sid = getSessionId(await t.client.createSession());
      t.provider.expectText({ id: 'fb-012-empty', text: '' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'empty then settle once');

      await waitForCondition(
        () => t.provider.syntheticRequests.filter((r) => r.marker === 'zwsp'),
        (requests) => requests.length >= 1,
        TIMEOUTS.quick,
      );
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      // Wait for a second idle-class event after the first continuation activity.
      // Host may emit session.idle + session.status idle; neither may double-inject.
      const firstIdle = await t.events.awaitEvent(
        (e) => e.sessionID === sid && e.type === 'session.idle',
        TIMEOUTS.quick,
      );
      await t.events.awaitEvent(
        (e) =>
          e.sessionID === sid
          && e.seq > firstIdle.seq
          && (e.type === 'session.idle'
            || (e.type === 'session.status' && (e.status === 'idle' || e.properties?.status?.type === 'idle'))),
        TIMEOUTS.quick,
      ).catch(() => null);

      // Settle: wait until host_accepted is durable, then re-check zwsp count.
      await waitForNdjson(
        t.host.workDir,
        (events) =>
          events.filter((e) => e.Session === sid && e.Kind === 'continuation_host_accepted').length === 1,
        TIMEOUTS.quick,
      );

      if (zwspCount(t) !== 1) {
        throw new Error(`Expected exactly one zwsp continuation after duplicate idle window, got ${zwspCount(t)}`);
      }

      const ndjson = await waitForNdjson(
        t.host.workDir,
        (events) => events.some((e) => e.Session === sid && e.Kind === 'continuation_host_accepted'),
        TIMEOUTS.quick,
      );
      const requested = continuationKinds(ndjson, sid).filter((e) => e.Kind === 'continuation_requested');
      const accepted = continuationKinds(ndjson, sid).filter((e) => e.Kind === 'continuation_host_accepted');
      if (requested.length !== 1) {
        throw new Error(`Expected 1 continuation_requested, got ${requested.length}`);
      }
      if (accepted.length !== 1) {
        throw new Error(`Expected 1 continuation_host_accepted, got ${accepted.length}`);
      }
      expectNoSessionError(t, sid);
    },
  },

  {
    name: 'OC-FB-011 duplicate empty-output error does not double-continue',
    fn: async (t) => {
      const sid = getSessionId(await t.client.createSession());
      // Two empty assistant finishes in one human turn should still yield one lease.
      t.provider.expectText({ id: 'fb-011-empty-a', text: '' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'first empty');
      await waitForCondition(
        () => t.provider.syntheticRequests.filter((r) => r.marker === 'zwsp'),
        (requests) => requests.length === 1,
        TIMEOUTS.quick,
      );
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const ndjson = await waitForNdjson(
        t.host.workDir,
        (events) =>
          events.filter((e) => e.Session === sid && e.Kind === 'continuation_requested').length >= 1,
        TIMEOUTS.quick,
      );
      const requested = continuationKinds(ndjson, sid).filter((e) => e.Kind === 'continuation_requested');
      if (requested.length !== 1) {
        throw new Error(`Expected single continuation_requested after empty-output path, got ${requested.length}`);
      }
      if (zwspCount(t) !== 1) {
        throw new Error(`Expected single zwsp, got ${zwspCount(t)}`);
      }
      expectNoSessionError(t, sid);
    },
  },
];

export default tests;
