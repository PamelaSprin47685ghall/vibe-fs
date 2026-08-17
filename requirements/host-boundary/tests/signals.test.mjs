process.env.WANXIANGSHU_NO_FATAL_EXIT = '1'

import assert from 'node:assert/strict'
import test from 'node:test'
import * as HostSignalSurface from '../../../dist/OpenCode/Host/HostSignalSurface.js'
import * as HostSignalSubscribeSurface from '../../../dist/OpenCode/Host/HostSignalSubscribeSurface.js'

const hostSignals = { tryDecode: (raw) => HostSignalSurface.tryDecode(raw) ?? undefined, tryAdapt: (owned, raw) => HostSignalSurface.tryAdapt(owned, raw) ?? undefined }

const idleRaw = (sessionId) => ({ type: 'session.status', sessionID: sessionId, properties: { status: { type: 'idle' } } })
const dedicatedIdleRaw = (sessionId) => ({ type: 'session.idle', properties: { sessionID: sessionId } })
const retryRaw = (sessionId) => ({ type: 'session.status', sessionID: sessionId, properties: { status: { type: 'retry', attempt: '2', message: 'rate limited' } } })
const deletedRaw = (sessionId, parentID) => ({ type: 'session.deleted', sessionID: sessionId, properties: { parentID } })
const errorRaw = (sessionId, name = 'TimeoutError') => ({ type: 'session.error', sessionID: sessionId, properties: { error: { name } } })

test('WHAT[HOST-BOUNDARY-003] MISC_signals_session_id_of_all_cases', () => {
  for (const raw of [idleRaw('s1'), retryRaw('s1'), deletedRaw('s1', 'root'), errorRaw('s1')]) assert.equal(hostSignals.tryDecode(raw).sessionId, 's1')
})

test('WHAT[HOST-BOUNDARY-002] MISC_signals_try_adapt_idle_retry_deleted_and_failure', () => {
  assert.equal(hostSignals.tryDecode(idleRaw('s1')).kind, 'SessionIdle')
  assert.equal(hostSignals.tryDecode(dedicatedIdleRaw('s1')).kind, 'SessionIdle')
  assert.equal(hostSignals.tryDecode(retryRaw('s1')).kind, 'ProviderRetry')
  assert.equal(hostSignals.tryDecode(deletedRaw('s1', 'owner-1')).kind, 'SessionDeleted')
  assert.equal(hostSignals.tryDecode(errorRaw('s1', 'OverloadedError')).kind, 'ProviderFailure')
  assert.equal(hostSignals.tryDecode({ event: idleRaw('s2') }).kind, 'SessionIdle')
  assert.equal(hostSignals.tryDecode({ payload: { ...idleRaw('s3'), sessionId: 's3' } }).kind, 'SessionIdle')
  assert.equal(hostSignals.tryDecode(null), undefined)
  assert.equal(hostSignals.tryDecode({ type: 'chat.message' }), undefined)
})

test('WHAT[HOST-BOUNDARY-002] R3_abort_error_adapts_to_attempt_aborted_not_dropped', () => {
  assert.equal(hostSignals.tryDecode(errorRaw('s1', 'AbortError')).kind, 'AttemptAborted')
})

test('WHAT[HOST-BOUNDARY-002] MISC_signals_try_adapt_ownership_gate', () => {
  // Production tryAdapt: unowned session signals are dropped except
  // ProviderFailure which always crosses (isOwned || signal is ProviderFailure).
  assert.equal(hostSignals.tryAdapt([], idleRaw('s1')), undefined)
  assert.notEqual(hostSignals.tryAdapt([], errorRaw('s1')), undefined)
  assert.notEqual(hostSignals.tryAdapt(['s1'], idleRaw('s1')), undefined)
})

test('WHAT[HOST-BOUNDARY-002] MISC_signals_router_register_unregister', () => {
  // Production tryAdapt uses the owned array as a set; register = add to
  // array, unregister = remove. The ownership gate is the production
  // adapter, not a test-side router.
  assert.notEqual(hostSignals.tryAdapt(['s1'], idleRaw('s1')), undefined)
  assert.notEqual(hostSignals.tryAdapt(['s1'], retryRaw('s1')), undefined)
  assert.equal(hostSignals.tryAdapt([], idleRaw('s1')), undefined)
})

test('WHAT[HOST-BOUNDARY-001] MISC_signals_router_loop_delta_bypasses_adapt', () => {
  // Loop text deltas are not coarse session lifecycle signals — the codec
  // drops them before adaptation. tryAdapt returns undefined.
  assert.equal(hostSignals.tryAdapt(['x'], { type: 'message.part.delta', properties: { sessionID: 'x' } }), undefined)
  // But a real signal for the same owned session does adapt.
  assert.notEqual(hostSignals.tryAdapt(['x'], errorRaw('x')), undefined)
})

// ── HOST-BOUNDARY-003: signal subscribe lifecycle ────────────────────────
//
// HostSignalSubscribeSurface.trySubscribe is the JS-native owner surface.
// It returns { ok: true, source, dispose } on success or
// { ok: false, error } on failure — no Fable Result representation.

const trySubscribe = async (input = {}) => HostSignalSubscribeSurface.trySubscribe(input, () => {}, null)

test('WHAT[HOST-BOUNDARY-003] MISC_signals_listen_subscription_lifecycle', async () => {
  let disposed = false
  const result = await HostSignalSubscribeSurface.trySubscribe(
    { events: { listen: () => () => { disposed = true } } },
    () => {},
    null,
  )
  assert.equal(result.ok, true)
  assert.equal(result.source, 'events.listen')
  result.dispose()
  assert.equal(disposed, true)
})

test('WHAT[HOST-BOUNDARY-003] MISC_signals_listen_error_paths', async () => {
  const noListen = await trySubscribe({ events: {} })
  assert.equal(noListen.ok, false)
  assert.match(noListen.error, /events\.listen unavailable/)

  const nullSubscription = await trySubscribe({ events: { listen: () => null } })
  assert.equal(nullSubscription.ok, false)
  assert.match(nullSubscription.error, /no subscription/)

  const throwingListen = await trySubscribe({ events: { listen: () => { throw new Error('listener boom') } } })
  assert.equal(throwingListen.ok, false)
  assert.match(throwingListen.error, /listener boom/)
})

test('WHAT[HOST-BOUNDARY-003] MISC_signals_invalid_callback_fails_closed', async () => {
  const result = await HostSignalSubscribeSurface.trySubscribe(
    { events: { listen: () => () => {} } },
    null,
    null,
  )
  assert.equal(result.ok, false)
  assert.match(result.error, /callback unavailable/)
})

test('WHAT[HOST-BOUNDARY-003] MISC_signals_default_input_resolves_to_local_event_hook', async () => {
  const result = await trySubscribe({})
  assert.equal(result.ok, true)
  assert.equal(result.source, 'local-event-hook')
})

test('WHAT[HOST-BOUNDARY-003] MISC_signals_client_events_listen_fallback', async () => {
  let called = false
  const result = await HostSignalSubscribeSurface.trySubscribe(
    { client: { events: { listen: () => () => { called = true } } } },
    () => {},
    null,
  )
  assert.equal(result.ok, true)
  assert.equal(result.source, 'events.listen')
  result.dispose()
  assert.equal(called, true)
})

test('WHAT[HOST-BOUNDARY-003] MISC_signals_server_url_ignored_in_favor_of_local_hook', async () => {
  const result = await trySubscribe({ serverUrl: 'http://localhost:4096' })
  assert.equal(result.ok, true)
  assert.equal(result.source, 'local-event-hook')
})

// ── Mutation sensitivity ─────────────────────────────────────────────────

test('WHAT[HOST-BOUNDARY-002] mutation_canary_ProviderFailure_crosses_without_ownership', () => {
  // ProviderFailure must always cross the boundary even for unowned sessions.
  // If someone adds an ownership gate to ProviderFailure, this canary fails.
  assert.notEqual(hostSignals.tryAdapt([], errorRaw('s1', 'OverloadedError')), undefined,
    'mutation guard: ProviderFailure must cross without ownership')
})
