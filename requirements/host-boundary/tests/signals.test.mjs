// Signals cluster: HostSignalAdapter tryAdapt/ownership routing + Router
// source semantics, HostSignalSubscribe listen/global subscription lifecycle.

import assert from 'node:assert/strict'
import test from 'node:test'

import { caseOf } from '../../verification-system/tests/support/domain.mjs'

const { SessionIdModule_create: sid } = await import('../../../dist/Foundation/Identity.js')
const {
  HostSignal,
  RetrySignal,
} = await import('../../../dist/OpenCode/Signals/HostSignal.js')
const adapter = await import('../../../dist/OpenCode/Signals/HostSignalAdapter.js')
const { HostSignalAdapter_sessionIdOf: sessionIdOf, HostSignalAdapter_tryAdapt: tryAdapt, HostSignalRouter } = adapter
// Resolve Fable-exported members by prefix; the hash suffix is a compiler
// artifact and must not be pinned in tests (VERIFY-008).
const memberOf = (name) => Object.entries(adapter).find(([k]) => k.startsWith(`HostSignalRouter__${name}_`))?.[1]
const RegisterOwned = memberOf('RegisterOwned')
const UnregisterOwned = memberOf('UnregisterOwned')
const Observe = memberOf('Observe')
const ObserveLocal = memberOf('ObserveLocal')
const isOwned = memberOf('isOwned')
const makeRouter = (...args) => new HostSignalRouter(...args)
const { trySubscribe } = await import('../../../dist/OpenCode/Signals/HostSignalSubscribe.js')


const idleRaw = (sessionID, extra = {}) => ({ type: 'session.status', sessionID, properties: { status: { type: 'idle' }, ...extra } })
const dedicatedIdleRaw = (sessionID) => ({ type: 'session.idle', properties: { sessionID } })
const retryRaw = (sessionID, attempt = '2', message = 'rate limited') => ({
  type: 'session.status',
  sessionID,
  properties: { status: { type: 'retry', attempt, message } },
})
const deletedRaw = (sessionID, parentID) => ({
  type: 'session.deleted',
  sessionID,
  properties: parentID ? { info: { id: sessionID, parentID } } : { info: { id: sessionID } },
})
const errorRaw = (sessionID, name = 'TimeoutError', message = 'slow') => ({
  type: 'session.error',
  sessionID,
  properties: { error: { name, message } },
})

// ── sessionIdOf ──────────────────────────────────────────────────────────────

test('MISC_signals_session_id_of_all_cases', () => {
  const s1 = sid('s1')
  assert.equal(sessionIdOf(new HostSignal(0, [s1])).fields[0], 's1')
  assert.equal(sessionIdOf(new HostSignal(1, [new RetrySignal(s1, '1', 'r')])).fields[0], 's1')
  assert.equal(sessionIdOf(new HostSignal(2, [s1, 'why'])).fields[0], 's1')
  assert.equal(sessionIdOf(new HostSignal(3, [s1])).fields[0], 's1')
})

// ── tryAdapt ─────────────────────────────────────────────────────────────────

test('MISC_signals_try_adapt_idle_retry_deleted_and_failure', () => {
  const owned = () => true
  assert.equal(caseOf(tryAdapt(owned, idleRaw('s1'))), 'SessionIdle')
  assert.equal(caseOf(tryAdapt(owned, dedicatedIdleRaw('s1'))), 'SessionIdle')
  assert.equal(caseOf(tryAdapt(owned, retryRaw('s1', '3', 'backoff'))), 'ProviderRetry')
  const deleted = tryAdapt(owned, deletedRaw('s1', 'owner-1'))
  assert.equal(caseOf(deleted), 'SessionDeleted')
  assert.equal(deleted.fields[0].fields[0], 's1')
  assert.equal(deleted.fields[1].fields[0], 'owner-1', 'session.deleted must preserve Host info.parentID')
  assert.equal(caseOf(tryAdapt(owned, errorRaw('s1', 'OverloadedError', 'busy'))), 'ProviderFailure')

  // Wrapped payload / event shapes unwrap too.
  assert.equal(caseOf(tryAdapt(owned, { event: idleRaw('s2') })), 'SessionIdle')
  assert.equal(caseOf(tryAdapt(owned, { payload: { type: 'session.status', sessionId: 's3', properties: { status: { type: 'idle' } } } })), 'SessionIdle')

  assert.equal(tryAdapt(owned, null), undefined)
  assert.equal(tryAdapt(owned, { type: 'chat.message' }), undefined)
  assert.equal(tryAdapt(owned, { type: 'session.status', properties: { status: { type: 'idle' } } }), undefined, 'missing session id drops')
  assert.equal(tryAdapt(owned, { type: 'session.status', sessionID: 's', properties: { status: { type: 'running' } } }), undefined, 'unknown status drops')
  // HOST-002/004: operator abort remains typed so the idle capability can be revoked.
  assert.equal(caseOf(tryAdapt(owned, { type: 'session.error', sessionID: 's', properties: { error: { name: 'AbortError' } } })), 'AttemptAborted', 'abort maps to AttemptAborted, not classified away')
})

// Given an upstream AbortError notification.
// Trigger: HostSignalAdapter decodes the event.
// Expected: typed AttemptAborted reaches capability revocation.
// Forbidden: dropping it or converting it to ProviderFailure (HOST-002/004).
test('R3_abort_error_adapts_to_attempt_aborted_not_dropped', () => {
  const owned = () => true
  const sig = tryAdapt(owned, {
    type: 'session.error',
    sessionID: 's',
    properties: { error: { name: 'AbortError' } },
  })
  assert.notEqual(sig, undefined, 'operator abort must remain a typed signal (HOST-002/004)')
  assert.equal(caseOf(sig), 'AttemptAborted')
})

test('MISC_signals_try_adapt_ownership_gate', () => {
  const notOwned = () => false
  assert.equal(tryAdapt(notOwned, idleRaw('s1')), undefined, 'foreign idle is dropped')
  assert.equal(tryAdapt(notOwned, retryRaw('s1')), undefined, 'foreign retry is dropped')
  assert.equal(tryAdapt(notOwned, deletedRaw('s1')), undefined, 'deleted still requires ownership')
  assert.equal(caseOf(tryAdapt(notOwned, errorRaw('s1'))), 'ProviderFailure', 'provider failure always passes')
})

// ── Router ───────────────────────────────────────────────────────────────────

test('MISC_signals_router_register_unregister', () => {
  const received = []
  const router = makeRouter(new Set(), (s) => received.push(s), undefined)
  const s1 = sid('s1')

  RegisterOwned(router, s1)
  assert.equal(isOwned(router, s1), true)

  Observe(router, idleRaw('s1'))
  assert.equal(received.length, 1)
  assert.equal(caseOf(received[0]), 'SessionIdle')

  ObserveLocal(router, retryRaw('s1', '1'))
  assert.equal(received.length, 2)
  assert.equal(caseOf(received[1]), 'ProviderRetry')

  // Unregister removes ownership.
  UnregisterOwned(router, s1)
  assert.equal(isOwned(router, s1), false)
  Observe(router, idleRaw('s1'))
  assert.equal(received.length, 2, 'unregistered session is foreign and dropped')
})

test('MISC_signals_router_loop_delta_bypasses_adapt', () => {
  const received = []
  const loopEvents = []
  const router = makeRouter(new Set(), (s) => received.push(s), (raw) => loopEvents.push(raw))
  Observe(router, { type: 'message.part.delta', part: { text: 'x' } })
  assert.equal(loopEvents.length, 1)
  assert.equal(received.length, 0)
  Observe(router, errorRaw('x1'))
  assert.equal(received.length, 1, 'non-loop signals still adapt and route')
})

// ── subscribeListen ──────────────────────────────────────────────────────────

test('MISC_signals_listen_subscription_lifecycle', async () => {
  let unsubscribed = false
  const events = { listen: () => () => { unsubscribed = true } }
  const result = await trySubscribe({ events }, (raw) => {}, undefined)
  assert.equal(result.tag, 0)
  const [sub, source] = result.fields[0]
  assert.equal(source, 'events.listen')
  assert.equal(sub.Health().IsConnected, true)
  assert.equal(sub.Health().ReconnectAttempts, 0)
  sub.Dispose()
  assert.equal(unsubscribed, true)
})

test('MISC_signals_listen_error_paths', async () => {
  const noListen = await trySubscribe({ events: {} }, () => {}, undefined)
  assert.equal(noListen.tag, 1)
  assert.match(noListen.fields[0], /events\.listen unavailable/)

  const nullSub = await trySubscribe({ events: { listen: () => null } }, () => {}, undefined)
  assert.equal(nullSub.tag, 1)
  assert.match(nullSub.fields[0], /returned no subscription/)

  const throwSub = await trySubscribe({ events: { listen: () => { throw new Error('listener boom') } } }, () => {}, undefined)
  assert.equal(throwSub.tag, 1)
  assert.match(throwSub.fields[0], /OPENCODE-SIGNAL-SUBSCRIBE: listener boom/)
})

// ── local-event-hook default ────────────────────────────────────────────────

test('MISC_signals_default_input_resolves_to_local_event_hook', async () => {
  const result = await trySubscribe({}, () => {}, undefined)
  assert.equal(result.tag, 0)
  const [sub, source] = result.fields[0]
  assert.equal(source, 'local-event-hook')
  assert.equal(sub, undefined)
})

test('MISC_signals_client_events_listen_fallback', async () => {
  let called = false
  const result = await trySubscribe({ client: { events: { listen: () => () => { called = true } } } }, () => {}, undefined)
  assert.equal(result.tag, 0)
  assert.equal(result.fields[0][1], 'events.listen')
  result.fields[0][0].Dispose()
  assert.equal(called, true)
})

test('MISC_signals_server_url_ignored_in_favor_of_local_hook', async () => {
  const result = await trySubscribe({ serverUrl: 'http://localhost:4096' }, () => {}, undefined)
  assert.equal(result.tag, 0)
  assert.equal(result.fields[0][1], 'local-event-hook')
})

