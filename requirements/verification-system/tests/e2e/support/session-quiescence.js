/**
 * session-quiescence.js — the Host's idle signal, owned once.
 *
 * Transport-level quiescence ONLY, never the product-correctness oracle: correctness evidence is
 * the reconciler outcome from the host API plus tool/git/journal assertions. This module exists
 * because the same predicate had been written twice (`scenario-turn.js`, `scenario-driver.mjs`)
 * with two different reaches — one restricted to `session.status`, one accepting a `status` field
 * on any event — so "is this session idle" had two answers depending on which file asked.
 */

import { DEFAULT_AWAIT_TIMEOUT_MS } from './time-budget.js';

/** The Host's idle signal: a `session.idle` event, or a `session.status` carrying idle. */
export function isIdleEvent(event) {
  if (!event) return false;
  if (event.type === 'session.idle') return true;
  if (event.type !== 'session.status') return false;
  const status = event.status ?? event.properties?.status;
  if (status === 'idle') return true;
  return Boolean(status) && typeof status === 'object' && (status.type === 'idle' || status.status === 'idle');
}

/** Which session an SSE event belongs to, or null. */
export function sessionIdOfEvent(event) {
  return event?.sessionID ?? event?.properties?.sessionID ?? null;
}

/**
 * Wait until a session is settled — the Host reports it idle, or stops reporting it at all.
 *
 * Event-driven, with exactly one confirming read: the subscription is registered BEFORE the read,
 * so an idle transition landing between them is caught by the subscription rather than lost, and
 * the read covers the case no event can ever cover — a session that was already idle before the
 * caller started watching.
 *
 * That case is why this is not a polling loop. The wait it replaces polled `/session/status` and
 * required observing a NON-idle status first, so an already-quiet session could never satisfy it:
 * every call burned its whole budget and threw. Paid once per session per scenario, it was the
 * largest single term in a multi-scenario case file's wall time.
 */
export async function awaitSessionSettled(scenario, sessionId, timeoutMs = DEFAULT_AWAIT_TIMEOUT_MS) {
  const settledEvent = scenario.events
    ? scenario.events
        .awaitEvent(
          (event) => isIdleEvent(event) && sessionIdOfEvent(event) === sessionId,
          timeoutMs,
        )
        .catch(() => null)
    : Promise.resolve(null);

  if (await isSettledNow(scenario.client, sessionId)) return true;
  return (await settledEvent) !== null;
}

/** One status read: settled when the Host reports idle, or does not report the session. */
async function isSettledNow(client, sessionId) {
  const response = await client.request('GET', '/session/status');
  if (!response.ok || !response.data) return false;
  const statuses = response.data.data || response.data || {};
  const status = statuses[sessionId];
  if (!status) return true;
  return status.type === 'idle' || status.status === 'idle';
}
