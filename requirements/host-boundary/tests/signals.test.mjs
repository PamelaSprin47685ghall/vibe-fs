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

// ── HOST-BOUNDARY-028: signal subscription membrane ─────────────────────
//
// HostSignalSubscribeSurface.trySubscribe is the JS-native owner surface.
// It returns { ok: true, mode, dispose } on success or
// { ok: false, error } on failure — no Fable Result representation.

const trySubscribe = async (input = {}) => HostSignalSubscribeSurface.trySubscribe(input, () => {})

test('WHAT[HOST-BOUNDARY-028] MISC_signals_subscription_mode_is_closed', async () => {
  const local = await trySubscribe({})
  assert.deepEqual(local, { ok: true, mode: 'LocalEventHook', dispose: null })

  let disposed = false
  const result = await HostSignalSubscribeSurface.trySubscribe(
    { events: { listen: () => () => { disposed = true } } },
    () => {},
  )
  assert.equal(result.mode, 'EventsListen')
  assert.equal(typeof result.dispose, 'function')
  result.dispose()
  assert.equal(disposed, true)
})

test('WHAT[HOST-BOUNDARY-028] MISC_signals_listener_capability_fails_closed', async () => {
  const noListen = await trySubscribe({ events: {} })
  assert.deepEqual(noListen, { ok: false, error: 'OPENCODE-SIGNAL-SUBSCRIBE: events.listen unavailable' })

  for (const listen of [null, 7, {}, 'listen']) {
    assert.deepEqual(await trySubscribe({ events: { listen } }), noListen)
  }

  const clientListen = () => () => {}
  assert.deepEqual(
    await trySubscribe({ events: 7, client: { events: { listen: clientListen } } }),
    { ok: false, error: 'OPENCODE-SIGNAL-SUBSCRIBE: invalid input' },
  )
})

test('WHAT[HOST-BOUNDARY-028] MISC_signals_disposer_capability_fails_closed', async () => {
  const expected = { ok: false, error: 'OPENCODE-SIGNAL-SUBSCRIBE: events.listen returned invalid disposer' }

  for (const disposer of [null, 7, {}, 'dispose', Promise.resolve()]) {
    assert.deepEqual(await trySubscribe({ events: { listen: () => disposer } }), expected)
  }
})

test('WHAT[HOST-BOUNDARY-028] MISC_signals_input_carriers_fail_closed', async () => {
  const expected = { ok: false, error: 'OPENCODE-SIGNAL-SUBSCRIBE: invalid input' }

  for (const input of [null, 7, 'input', [], new String('input'), new Date(0)]) {
    assert.deepEqual(await trySubscribe(input), expected)
  }

  for (const input of [
    { events: [] },
    { events: 'events' },
    { client: 7 },
    { client: [] },
    { client: new String('client') },
    { client: new Date(0) },
    { client: { events: [] } },
  ]) {
    assert.deepEqual(await trySubscribe(input), expected)
  }

  assert.deepEqual(await trySubscribe({ events: null, client: null }), { ok: true, mode: 'LocalEventHook', dispose: null })
})

test('WHAT[HOST-BOUNDARY-028] MISC_signals_listener_throw_is_typed', async () => {
  const result = await trySubscribe({ events: { listen: () => { throw new Error('listener boom') } } })
  assert.deepEqual(result, { ok: false, error: 'OPENCODE-SIGNAL-SUBSCRIBE: events.listen failed: listener boom' })

  for (const thrown of [null, 7, 'wire boom']) {
    const adjacent = await trySubscribe({ events: { listen: () => { throw thrown } } })
    assert.equal(adjacent.ok, false)
    assert.match(adjacent.error, /^OPENCODE-SIGNAL-SUBSCRIBE: events\.listen failed:/)
  }
})

test('WHAT[HOST-BOUNDARY-028] MISC_signals_throwing_accessors_resolve_typed_failure', async () => {
  const topLevelEvents = {}
  Object.defineProperty(topLevelEvents, 'events', { get: () => { throw new Error('events getter boom') } })
  assert.deepEqual(await trySubscribe(topLevelEvents), { ok: false, error: 'OPENCODE-SIGNAL-SUBSCRIBE: invalid input' })

  const topLevelClient = {}
  Object.defineProperty(topLevelClient, 'client', { get: () => { throw new Error('client getter boom') } })
  assert.deepEqual(await trySubscribe(topLevelClient), { ok: false, error: 'OPENCODE-SIGNAL-SUBSCRIBE: invalid input' })

  const client = {}
  Object.defineProperty(client, 'events', { get: () => { throw new Error('client events getter boom') } })
  assert.deepEqual(await trySubscribe({ client }), { ok: false, error: 'OPENCODE-SIGNAL-SUBSCRIBE: invalid input' })

  const events = {}
  Object.defineProperty(events, 'listen', { get: () => { throw new Error('listen getter boom') } })
  assert.deepEqual(
    await trySubscribe({ events }),
    { ok: false, error: 'OPENCODE-SIGNAL-SUBSCRIBE: events.listen failed: listen getter boom' },
  )

  const proxy = new Proxy({}, { get: () => { throw new Error('proxy boom') } })
  assert.deepEqual(await trySubscribe(proxy), { ok: false, error: 'OPENCODE-SIGNAL-SUBSCRIBE: invalid input' })
})

test('WHAT[HOST-BOUNDARY-028] MISC_signals_disposer_throw_reaches_resource_owner', async () => {
  const result = await trySubscribe({ events: { listen: () => () => { throw new Error('dispose boom') } } })
  assert.equal(result.mode, 'EventsListen')
  assert.throws(() => result.dispose(), /dispose boom/)
})

test('WHAT[HOST-BOUNDARY-028] MISC_signals_invalid_callback_fails_closed_at_surface', async () => {
  const result = await HostSignalSubscribeSurface.trySubscribe(
    { events: { listen: () => () => {} } },
    null,
  )
  assert.equal(result.ok, false)
  assert.match(result.error, /callback unavailable/)
})

test('WHAT[HOST-BOUNDARY-028] MISC_signals_default_input_resolves_to_local_event_hook', async () => {
  const result = await trySubscribe({})
  assert.deepEqual(result, { ok: true, mode: 'LocalEventHook', dispose: null })
})

test('WHAT[HOST-BOUNDARY-028] MISC_signals_opencode_class_client_without_legacy_events_uses_local_hook', async () => {
  class OpenCodeClient {
    constructor(events) { this.events = events }
  }

  assert.deepEqual(
    await trySubscribe({ client: new OpenCodeClient(undefined) }),
    { ok: true, mode: 'LocalEventHook', dispose: null },
  )

  let disposed = false
  const legacy = await HostSignalSubscribeSurface.trySubscribe(
    { client: new OpenCodeClient({ listen: () => () => { disposed = true } }) },
    () => {},
  )
  assert.equal(legacy.mode, 'EventsListen')
  legacy.dispose()
  assert.equal(disposed, true)
})

test('WHAT[HOST-BOUNDARY-028] MISC_signals_client_events_listen_fallback', async () => {
  let called = false
  const result = await HostSignalSubscribeSurface.trySubscribe(
    { client: { events: { listen: () => () => { called = true } } } },
    () => {},
  )
  assert.equal(result.mode, 'EventsListen')
  result.dispose()
  assert.equal(called, true)
})

test('WHAT[HOST-BOUNDARY-028] MISC_signals_server_url_ignored_in_favor_of_local_hook', async () => {
  const result = await trySubscribe({ serverUrl: 'http://localhost:4096' })
  assert.deepEqual(result, { ok: true, mode: 'LocalEventHook', dispose: null })
})

// ── Mutation sensitivity ─────────────────────────────────────────────────

test('WHAT[HOST-BOUNDARY-002] mutation_canary_ProviderFailure_crosses_without_ownership', () => {
  // ProviderFailure must always cross the boundary even for unowned sessions.
  // If someone adds an ownership gate to ProviderFailure, this canary fails.
  assert.notEqual(HostSignalSurface.tryAdapt([], errorRaw('s1', 'OverloadedError')), null,
    'mutation guard: ProviderFailure must cross without ownership')
})
