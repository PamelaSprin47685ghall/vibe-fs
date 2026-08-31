process.env.WANXIANGSHU_NO_FATAL_EXIT = '1'

import assert from 'node:assert/strict'
import test from 'node:test'
import * as HostSignalSurface from '../../../dist/OpenCode/Host/HostSignalSurface.js'
import * as HostSignalSubscribeSurface from '../../../dist/OpenCode/Host/HostSignalSubscribeSurface.js'

const idleRaw = (sessionId) => ({ type: 'session.status', sessionID: sessionId, properties: { status: { type: 'idle' } } })
const dedicatedIdleRaw = (sessionId) => ({ type: 'session.idle', properties: { sessionID: sessionId } })
const retryRaw = (sessionId) => ({ type: 'session.status', sessionID: sessionId, properties: { status: { type: 'retry', attempt: '2', message: 'rate limited' } } })
const deletedRaw = (sessionId, parentID) => ({ type: 'session.deleted', sessionID: sessionId, properties: { parentID } })
const errorRaw = (sessionId, name = 'TimeoutError') => ({ type: 'session.error', sessionID: sessionId, properties: { error: { name } } })

test('WHAT[HOST-BOUNDARY-003] MISC_signals_session_id_of_all_cases', () => {
  for (const raw of [idleRaw('s1'), retryRaw('s1'), deletedRaw('s1', 'root'), errorRaw('s1')]) {
    assert.equal(HostSignalSurface.tryDecode(raw).sessionId, 's1')
  }
})

test('WHAT[HOST-BOUNDARY-002] the host signal boundary exposes the exact typed coarse-signal set', () => {
  assert.deepEqual(HostSignalSurface.tryDecode(idleRaw('s1')), {
    kind: 'SessionIdle',
    sessionId: 's1',
  })
  assert.deepEqual(HostSignalSurface.tryDecode(dedicatedIdleRaw('s1')), {
    kind: 'SessionIdle',
    sessionId: 's1',
  })
  assert.deepEqual(HostSignalSurface.tryDecode(retryRaw('s1')), {
    kind: 'ProviderRetry',
    sessionId: 's1',
    attempt: '2',
    failure: 'ProviderTransient',
    diagnostic: 'rate limited',
  })
  assert.deepEqual(HostSignalSurface.tryDecode(deletedRaw('s1', 'owner-1')), {
    kind: 'SessionDeleted',
    sessionId: 's1',
    parentSessionId: 'owner-1',
  })
  assert.deepEqual(HostSignalSurface.tryDecode(errorRaw('s1', 'OverloadedError')), {
    kind: 'ProviderFailure',
    sessionId: 's1',
    failure: 'ProviderTransient',
    diagnostic: 'provider failure',
  })
  assert.deepEqual(HostSignalSurface.tryDecode(errorRaw('s1', 'AbortError')), {
    kind: 'AttemptAborted',
    sessionId: 's1',
    failure: 'UserCancelled',
    diagnostic: 'provider failure',
  })
  assert.deepEqual(HostSignalSurface.tryDecode({ event: idleRaw('s2') }), {
    kind: 'SessionIdle',
    sessionId: 's2',
  })
  assert.deepEqual(HostSignalSurface.tryDecode({ payload: { ...idleRaw('s3'), sessionId: 's3' } }), {
    kind: 'SessionIdle',
    sessionId: 's3',
  })
  assert.equal(HostSignalSurface.tryDecode(null), null)
  assert.equal(HostSignalSurface.tryDecode({ type: 'chat.message', sessionID: 's1' }), null)
  assert.equal(HostSignalSurface.tryDecode({ type: 'session.status', sessionID: 's1', properties: { status: { type: 'busy' } } }), null)
})

test('WHAT[HOST-BOUNDARY-002] R3_abort_error_adapts_to_attempt_aborted_not_dropped', () => {
  assert.deepEqual(HostSignalSurface.tryDecode(errorRaw('s1', 'MessageAbortedError')), {
    kind: 'AttemptAborted',
    sessionId: 's1',
    failure: 'UserCancelled',
    diagnostic: 'provider failure',
  })
  assert.deepEqual(HostSignalSurface.tryAdapt(['s1'], errorRaw('s1', 'AbortError')), {
    kind: 'AttemptAborted',
    sessionId: 's1',
    failure: 'UserCancelled',
    diagnostic: 'provider failure',
  })
  assert.equal(HostSignalSurface.tryAdapt([], errorRaw('s1', 'AbortError')), null)
  assert.deepEqual(HostSignalSurface.tryAdapt([], errorRaw('s1', 'OverloadedError')), {
    kind: 'ProviderFailure',
    sessionId: 's1',
    failure: 'ProviderTransient',
    diagnostic: 'provider failure',
  })
})

test('WHAT[HOST-BOUNDARY-002] MISC_signals_try_adapt_ownership_gate', () => {
  // Production tryAdapt: unowned session signals are dropped except
  // ProviderFailure which always crosses (isOwned || signal is ProviderFailure).
  assert.equal(HostSignalSurface.tryAdapt([], idleRaw('s1')), null)
  assert.notEqual(HostSignalSurface.tryAdapt([], errorRaw('s1')), null)
  assert.notEqual(HostSignalSurface.tryAdapt(['s1'], idleRaw('s1')), null)
})

test('WHAT[HOST-BOUNDARY-002] MISC_signals_router_register_unregister', () => {
  // Production tryAdapt uses the owned array as a set; register = add to
  // array, unregister = remove. The ownership gate is the production
  // adapter, not a test-side router.
  assert.notEqual(HostSignalSurface.tryAdapt(['s1'], idleRaw('s1')), null)
  assert.notEqual(HostSignalSurface.tryAdapt(['s1'], retryRaw('s1')), null)
  assert.equal(HostSignalSurface.tryAdapt([], idleRaw('s1')), null)
})

test('WHAT[HOST-BOUNDARY-001] MISC_signals_router_loop_delta_bypasses_adapt', () => {
  // Loop text deltas are not coarse session lifecycle signals — the codec
  // drops them before adaptation. tryAdapt returns null.
  assert.equal(HostSignalSurface.tryAdapt(['x'], { type: 'message.part.delta', properties: { sessionID: 'x' } }), null)
  // But a real signal for the same owned session does adapt.
  assert.notEqual(HostSignalSurface.tryAdapt(['x'], errorRaw('x')), null)
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
  assert.notEqual(HostSignalSurface.tryAdapt([], errorRaw('s1', 'OverloadedError')), null,
    'mutation guard: ProviderFailure must cross without ownership')
})
