// Signals cluster: HostSignalAdapter tryAdapt/ownership routing + Router
// source semantics, HostSignalSubscribe listen/global subscription lifecycle.

import assert from 'node:assert/strict'
import test from 'node:test'

import { caseOf } from '../support/domain.mjs'

const { SessionIdModule_create: sid } = await import('../../../dist/Kernel/Identity.js')
const {
  HostSignal,
  RetrySignal,
  SessionSignalSource,
} = await import('../../../dist/Infrastructure/OpenCode/Signals/HostSignal.js')
const {
  HostSignalAdapter_sessionIdOf: sessionIdOf,
  HostSignalAdapter_tryAdapt: tryAdapt,
  HostSignalRouter_$ctor_3DF87E56: makeRouter,
  HostSignalRouter__RegisterOwned_Z31B28506: RegisterOwned,
  HostSignalRouter__RegisterSource_741286B4: RegisterSource,
  HostSignalRouter__UnregisterOwned_Z31B28506: UnregisterOwned,
  HostSignalRouter__ObserveLocal_4E60E31B: ObserveLocal,
  HostSignalRouter__ObserveGlobal_4E60E31B: ObserveGlobal,
  HostSignalRouter__isOwned_Z31B28506: isOwned,
} = await import('../../../dist/Infrastructure/OpenCode/Signals/HostSignalAdapter.js')
const { trySubscribe } = await import('../../../dist/Infrastructure/OpenCode/Signals/HostSignalSubscribe.js')

const idleRaw = (sessionID, extra = {}) => ({ type: 'session.status', sessionID, properties: { status: { type: 'idle' }, ...extra } })
const retryRaw = (sessionID, attempt = '2', message = 'rate limited') => ({
  type: 'session.status',
  sessionID,
  properties: { status: { type: 'retry', attempt, message } },
})
const deletedRaw = (sessionID) => ({ type: 'session.deleted', sessionID })
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
  assert.equal(caseOf(tryAdapt(owned, retryRaw('s1', '3', 'backoff'))), 'ProviderRetry')
  assert.equal(caseOf(tryAdapt(owned, deletedRaw('s1'))), 'SessionDeleted')
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

test('MISC_signals_router_register_unregister_and_source_drop', () => {
  const received = []
  const router = makeRouter(new Set(), (s) => received.push(s), undefined)
  const s1 = sid('s1')

  RegisterOwned(router, s1)
  assert.equal(isOwned(router, s1), true)

  // Owned + global source → delivered.
  ObserveGlobal(router, idleRaw('s1'))
  assert.equal(received.length, 1)
  assert.equal(caseOf(received[0]), 'SessionIdle')

  // A session registered as a global-directory source drops local observation.
  RegisterSource(router, s1, SessionSignalSource.GlobalForeignDirectoryEvent)
  ObserveLocal(router, idleRaw('s1'))
  assert.equal(received.length, 1, 'signals from the owning source are dropped')

  // Global observation still arrives for that session.
  ObserveGlobal(router, idleRaw('s1'))
  assert.equal(received.length, 2)

  // Unregister removes both maps.
  UnregisterOwned(router, s1)
  assert.equal(isOwned(router, s1), false)
  ObserveGlobal(router, idleRaw('s1'))
  assert.equal(received.length, 2, 'unregistered session is foreign again')
})

test('MISC_signals_router_loop_delta_bypasses_adapt', () => {
  const received = []
  const loopEvents = []
  const router = makeRouter(new Set(), (s) => received.push(s), (raw) => loopEvents.push(raw))
  ObserveGlobal(router, { type: 'message.part.delta', part: { text: 'x' } })
  assert.equal(loopEvents.length, 1)
  assert.equal(received.length, 0)
  ObserveGlobal(router, errorRaw('x1'))
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

// ── subscribeGlobalEvent ─────────────────────────────────────────────────────

const controllableTimer = () => {
  const pending = []
  return {
    Delay: (ms) => {
      const handle = { _cancelled: false }
      handle.Cancel = () => { handle._cancelled = true }
      handle.Delay = new Promise((resolve) => pending.push(() => { if (!handle._cancelled) resolve() }))
      return handle
    },
    fire: () => { for (const r of pending.splice(0)) r() },
    Dispose: () => {},
  }
}

const tick = () => new Promise((resolve) => setTimeout(resolve, 5))

test('MISC_signals_global_subscribe_delivers_and_disposes', async () => {
  const events = []
  let release
  const gate = new Promise((resolve) => { release = resolve })
  const stream = (async function* () {
    yield idleRaw('s9')
    yield retryRaw('s9', '1', 'slow down')
    await gate
    yield idleRaw('s9')
  })()
  const client = { global: { event: async () => ({ stream }) } }
  const timer = controllableTimer()
  const result = await trySubscribe({ client }, (raw) => events.push(raw), timer)
  assert.equal(result.tag, 0)
  const [sub, source] = result.fields[0]
  assert.equal(source, 'global.event')

  await tick()
  assert.equal(events.length, 2, 'both stream events delivered')
  assert.equal(events[0].properties.status.type, 'idle')
  assert.equal(events[1].sessionID, 's9')

  const health = sub.Health()
  assert.equal(health.IsConnected, true)
  assert.ok(health.LastEventReceived !== undefined)

  sub.Dispose()
  release()
  const after = sub.Health()
  assert.equal(after.IsConnected, false, 'dispose flips health to disconnected')
  timer.fire()
  await tick()
})

test('MISC_signals_global_subscribe_stream_unavailable_and_error', async () => {
  const timer = controllableTimer()
  const badStream = await trySubscribe({ client: { global: { event: async () => ({}) } } }, () => {}, timer)
  assert.equal(badStream.tag, 0)
  await tick()
  const health = badStream.fields[0][0].Health()
  assert.match(health.LastError ?? '', /stream unavailable/)
  badStream.fields[0][0].Dispose()

  const timer2 = controllableTimer()
  const failing = { global: { event: async () => { throw new Error('sse down') } } }
  const errSub = await trySubscribe({ client: failing }, () => {}, timer2)
  await tick()
  const errHealth = errSub.fields[0][0].Health()
  assert.match(errHealth.LastError ?? '', /sse down/)
  errSub.fields[0][0].Dispose()
})

test('MISC_signals_global_subscribe_client_errors', async () => {
  const noClient = await trySubscribe({}, () => {}, undefined)
  assert.equal(noClient.tag, 1)
  assert.match(noClient.fields[0], /no client for global event/)

  const noGlobal = await trySubscribe({ client: {} }, () => {}, undefined)
  assert.equal(noGlobal.tag, 1)
  assert.match(noGlobal.fields[0], /\/global\/event unavailable/)
})

test('MISC_signals_client_events_listen_fallback', async () => {
  let called = false
  const result = await trySubscribe({ client: { events: { listen: () => () => { called = true } } } }, () => {}, undefined)
  assert.equal(result.tag, 0)
  assert.equal(result.fields[0][1], 'events.listen')
  result.fields[0][0].Dispose()
  assert.equal(called, true)
})

test('MISC_signals_local_subscription_precedes_global_when_both_available', async () => {
  let globalCalls = 0
  let unsubscribed = false
  const stream = (async function* () { yield idleRaw('local-first') })()
  const result = await trySubscribe({
    events: { listen: () => () => { unsubscribed = true } },
    client: { global: { event: async () => { globalCalls += 1; return { stream } } } },
  }, () => {}, undefined)

  assert.equal(result.tag, 0)
  assert.equal(result.fields[0][1], 'events.listen')
  assert.equal(globalCalls, 0, 'local subscription prevents cross-instance global connection')
  result.fields[0][0].Dispose()
  assert.equal(unsubscribed, true)
})

test('MISC_signals_server_url_probe_healthy_then_global', async () => {
  const originalFetch = globalThis.fetch
  globalThis.fetch = async (target) => ({
    ok: true,
    json: async () => ({ healthy: true }),
  })
  try {
    const events = []
    const stream = (async function* () { yield idleRaw('p1') })()
    const timer = controllableTimer()
    const result = await trySubscribe({ client: { global: { event: async () => ({ stream }) } }, serverUrl: 'http://localhost:9999' }, (raw) => events.push(raw), timer)
    assert.equal(result.tag, 0)
    assert.equal(result.fields[0][1], 'global.event')
    await tick()
    assert.equal(events.length, 1)
    result.fields[0][0].Dispose()
  } finally {
    globalThis.fetch = originalFetch
  }
})

test('MISC_signals_server_url_probe_unhealthy_falls_back_to_listen', async () => {
  const originalFetch = globalThis.fetch
  globalThis.fetch = async () => ({ ok: false, json: async () => ({}) })
  try {
    let unsubscribed = false
    const result = await trySubscribe(
      { serverUrl: 'http://localhost:9998', events: { listen: () => () => { unsubscribed = true } } },
      () => {},
      undefined,
    )
    assert.equal(result.tag, 0)
    assert.equal(result.fields[0][1], 'events.listen')
    result.fields[0][0].Dispose()
    assert.equal(unsubscribed, true)
  } finally {
    globalThis.fetch = originalFetch
  }
})
