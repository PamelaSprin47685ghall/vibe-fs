/**
 * p0-canary-tests-fallback-errors.js — Fallback error-class / duplicate-idle P0.
 * Real oracles: syntheticRequests count, EventProbe error/idle, NDJSON when present.
 *
 * OpenCode loads plugins lazily on first session traffic; empty-output /
 * error tests MUST warmup with a normal turn so Fallback hooks are live
 * before the critical prompt.
 */

import { getSessionId } from '../../../testkit/opencode/scenario.js';
import { waitForCondition, waitForNdjson } from './p0-canary-ndjson.js';
import { TIMEOUTS } from './p0-canary-utils.js';

function zwspCount(t) {
  return t.provider.syntheticRequests.filter((r) => r.marker === 'zwsp').length;
}

function continuationKinds(events, sid) {
  return events.filter((e) => e.Session === sid && String(e.Kind || '').startsWith('continuation_'));
}

function isIdleEv(e, sid) {
  if (e.sessionID !== sid) return false;
  if (e.type === 'session.idle') return true;
  if (e.type === 'session.status') {
    const s = e.status ?? e.properties?.status;
    if (s === 'idle') return true;
    if (s && typeof s === 'object') return s.type === 'idle' || s.status === 'idle';
  }
  return false;
}

/** Warm the host so plugin hooks + tool registry are live before critical path. */
async function warmupSession(t, sid) {
  const cmds = await t.client.request('GET', '/command');
  if (!cmds.ok) throw new Error(`GET /command failed: ${cmds.status}`);
  const names = (cmds.data || []).map((c) => c.name);
  if (!names.includes('loop')) {
    throw new Error(`plugin not ready: loop missing in ${names.join(',')}`);
  }
  t.provider.expectText({ id: `warmup-${sid.slice(-6)}`, text: 'warm' });
  const turn = await t.turn.start(sid);
  await t.client.prompt(sid, 'say warm');
  await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });
  // Plugin tools must have appeared on the warmup LLM request.
  const req = t.provider.requests[t.provider.requests.length - 1];
  const toolNames = (req?.tools || []).map((x) => x?.function?.name || x?.name).filter(Boolean);
  if (!toolNames.includes('write')) {
    throw new Error(`warmup did not expose plugin tools: ${toolNames.join(',')}`);
  }
}

const tests = [
  {
    name: 'OC-FB-007 non-retryable provider error does not continue',
    fn: async (t) => {
      const sid = getSessionId(await t.client.createSession());
      await warmupSession(t, sid);

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
      const beforeZwsp = zwspCount(t);
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'trigger non-retryable');
      const err = await t.events.awaitEvent(
        (e) => e.sessionID === sid && e.type === 'session.error',
        TIMEOUTS.prompt,
      );
      await t.events.awaitEvent((e) => isIdleEv(e, sid) && e.seq >= err.seq, TIMEOUTS.prompt);

      if (zwspCount(t) !== beforeZwsp) {
        throw new Error(`Expected no new zwsp after non-retryable, before=${beforeZwsp} after=${zwspCount(t)}`);
      }
      const events = await waitForNdjson(t.host.workDir, () => true, 2000).catch(() => []);
      const requested = continuationKinds(events, sid).filter((e) => e.Kind === 'continuation_requested');
      if (requested.length !== 0) {
        throw new Error(`non-retryable must not request continuation, got ${requested.length}`);
      }
      if (t.provider.unexpectedRequests.length !== 0) {
        throw new Error(`unexpected LLM retries after non-retryable: ${t.provider.unexpectedRequests.length}`);
      }
    },
  },

  {
    name: 'OC-FB-008 MessageAbortedError does not continue',
    fn: async (t) => {
      const sid = getSessionId(await t.client.createSession());
      await warmupSession(t, sid);
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
      const beforeZwsp = zwspCount(t);
      await t.turn.start(sid);
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
        (e) => isIdleEv(e, sid) || (e.sessionID === sid && e.type === 'session.error'),
        TIMEOUTS.prompt,
      );

      if (zwspCount(t) !== beforeZwsp) {
        throw new Error(`abort must not dispatch zwsp continuation, before=${beforeZwsp} after=${zwspCount(t)}`);
      }
      const events = await waitForNdjson(t.host.workDir, () => true, 2000).catch(() => []);
      const requested = continuationKinds(events, sid).filter((e) => e.Kind === 'continuation_requested');
      if (requested.length !== 0) {
        throw new Error(`MessageAborted must not request continuation, got ${JSON.stringify(requested)}`);
      }
    },
  },

  {
    name: 'OC-FB-012 duplicate idle does not double-continue',
    fn: async (t) => {
      const sid = getSessionId(await t.client.createSession());
      await warmupSession(t, sid);
      t.provider.expectText({ id: 'fb-012-empty', text: '', emptyAssistant: true });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'empty then settle once');

      await waitForCondition(
        () => t.provider.syntheticRequests.filter((r) => r.marker === 'zwsp'),
        (requests) => requests.length >= 1,
        TIMEOUTS.prompt,
      );
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const firstIdle = await t.events.awaitEvent(
        (e) => e.sessionID === sid && e.type === 'session.idle',
        TIMEOUTS.quick,
      );
      await t.events.awaitEvent(
        (e) => isIdleEv(e, sid) && e.seq > firstIdle.seq,
        TIMEOUTS.quick,
      ).catch(() => null);

      const ndjson = await waitForNdjson(
        t.host.workDir,
        (events) =>
          events.some((e) => e.Session === sid && e.Kind === 'continuation_host_accepted'),
        TIMEOUTS.prompt,
      );
      if (zwspCount(t) !== 1) {
        throw new Error(`Expected exactly one zwsp continuation after idle window, got ${zwspCount(t)}`);
      }
      const requested = continuationKinds(ndjson, sid).filter((e) => e.Kind === 'continuation_requested');
      const accepted = continuationKinds(ndjson, sid).filter((e) => e.Kind === 'continuation_host_accepted');
      if (requested.length !== 1) {
        throw new Error(`Expected 1 continuation_requested, got ${requested.length}`);
      }
      if (accepted.length !== 1) {
        throw new Error(`Expected 1 continuation_host_accepted, got ${accepted.length}`);
      }
    },
  },

  {
    name: 'OC-FB-011 duplicate empty-output path does not double-continue',
    fn: async (t) => {
      const sid = getSessionId(await t.client.createSession());
      await warmupSession(t, sid);
      t.provider.expectText({ id: 'fb-011-empty-a', text: '', emptyAssistant: true });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'first empty');
      await waitForCondition(
        () => t.provider.syntheticRequests.filter((r) => r.marker === 'zwsp'),
        (requests) => requests.length === 1,
        TIMEOUTS.prompt,
      );
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.prompt });

      const ndjson = await waitForNdjson(
        t.host.workDir,
        (events) =>
          events.filter((e) => e.Session === sid && e.Kind === 'continuation_requested').length >= 1,
        TIMEOUTS.prompt,
      );
      const requested = continuationKinds(ndjson, sid).filter((e) => e.Kind === 'continuation_requested');
      if (requested.length !== 1) {
        throw new Error(`Expected single continuation_requested, got ${requested.length}`);
      }
      if (zwspCount(t) !== 1) {
        throw new Error(`Expected single zwsp, got ${zwspCount(t)}`);
      }
    },
  },
];

export default tests;
