import assert from 'node:assert/strict'
import test from 'node:test'
import * as HostSignalSurface from '../../../dist/OpenCode/Host/HostSignalSurface.js'
import { hostSignalSubscribe, signalRouter } from './support/host-surface.mjs'

const hostSignals = { tryDecode: (raw) => HostSignalSurface.tryDecode(raw) ?? undefined }

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
  assert.equal(hostSignals.tryDecode(errorRaw('s', 'AbortError')).kind, 'AttemptAborted')
})

test('WHAT[HOST-BOUNDARY-002] MISC_signals_try_adapt_ownership_gate', () => {
  const received = []
  const router = signalRouter([], (value) => received.push(value))
  router.observe(idleRaw('s1'))
  assert.equal(received.length, 0)
  router.observe(errorRaw('s1'))
  assert.equal(received.length, 1)
  router.register('s1')
  router.observe(idleRaw('s1'))
  assert.equal(received.length, 2)
})

test('WHAT[HOST-BOUNDARY-002] MISC_signals_router_register_unregister', () => {
  const received = []
  const router = signalRouter([], (value) => received.push(value))
  router.register('s1')
  assert.equal(router.isOwned('s1'), true)
  router.observe(idleRaw('s1'))
  router.observe(retryRaw('s1'))
  assert.deepEqual(received.map((value) => value.kind), ['SessionIdle', 'ProviderRetry'])
  router.unregister('s1')
  assert.equal(router.isOwned('s1'), false)
  router.observe(idleRaw('s1'))
  assert.equal(received.length, 2)
})

test('WHAT[HOST-BOUNDARY-001] MISC_signals_router_loop_delta_bypasses_adapt', () => {
  const received = []
  const router = signalRouter(['x'], (value) => received.push(value))
  router.observe({ type: 'message.part.delta', properties: { sessionID: 'x' } })
  assert.equal(received.length, 0)
  router.observe(errorRaw('x'))
  assert.equal(received.length, 1)
})

test('WHAT[HOST-BOUNDARY-003] MISC_signals_listen_subscription_lifecycle', async () => {
  let disposed = false
  const result = await hostSignalSubscribe.trySubscribe({ events: { listen: () => () => { disposed = true } } })
  assert.equal(result.ok, true)
  assert.equal(result.source, 'events.listen')
  result.subscription.dispose()
  assert.equal(disposed, true)
})

test('WHAT[HOST-BOUNDARY-003] MISC_signals_listen_error_paths', async () => {
  assert.equal((await hostSignalSubscribe.trySubscribe({ events: {} })).ok, false)
  assert.match((await hostSignalSubscribe.trySubscribe({ events: {} })).error, /events\.listen unavailable/)
  assert.match((await hostSignalSubscribe.trySubscribe({ events: { listen: () => null } })).error, /no subscription/)
  assert.match((await hostSignalSubscribe.trySubscribe({ events: { listen: () => { throw new Error('listener boom') } } })).error, /listener boom/)
})

test('WHAT[HOST-BOUNDARY-003] MISC_signals_default_input_resolves_to_local_event_hook', async () => {
  const result = await hostSignalSubscribe.trySubscribe({})
  assert.equal(result.ok, true)
  assert.equal(result.source, 'local-event-hook')
})

test('WHAT[HOST-BOUNDARY-003] MISC_signals_client_events_listen_fallback', async () => {
  let called = false
  const result = await hostSignalSubscribe.trySubscribe({ client: { events: { listen: () => () => { called = true } } } })
  assert.equal(result.ok, true)
  assert.equal(result.source, 'events.listen')
  result.subscription.dispose()
  assert.equal(called, true)
})

test('WHAT[HOST-BOUNDARY-003] MISC_signals_server_url_ignored_in_favor_of_local_hook', async () => {
  const result = await hostSignalSubscribe.trySubscribe({ serverUrl: 'http://localhost:4096' })
  assert.equal(result.ok, true)
  assert.equal(result.source, 'local-event-hook')
})
