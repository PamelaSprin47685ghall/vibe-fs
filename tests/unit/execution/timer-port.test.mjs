// tests/unit/execution/timer-port.test.mjs — ITimerPort contract (VERIFY-004)
// + SSE one-shot silence deadline via injected virtual timer (Proof plan #4).

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  diagnostic,
  hostSignalSubscribe,
  isNone,
  resultOf,
  timerPort,
} from '../support/domain.mjs'

const settle = () => new Promise((r) => setImmediate(r))
/** Drain a few microtask/macrotask turns so emitJsExpr async loops progress. */
const settleAll = async (n = 5) => {
  for (let i = 0; i < n; i += 1) await settle()
}

/** Production SSE heartbeat timeout (HostSignalSubscribe.HeartbeatTimeoutMs). */
const HEARTBEAT_TIMEOUT_MS = 30_000

/**
 * Controllable async SSE stream: push() yields events; end() closes the iterator.
 * global.event resolves to this stream shape expected by HostSignalSubscribe.
 */
const createControllableStream = () => {
  const queue = []
  let wait = null
  let done = false

  const wake = () => {
    if (wait) {
      const resolve = wait
      wait = null
      resolve()
    }
  }

  return {
    push(value) {
      queue.push(value)
      wake()
    },
    end() {
      done = true
      wake()
    },
    stream: {
      [Symbol.asyncIterator]() {
        return {
          async next() {
            while (queue.length === 0 && !done) {
              await new Promise((resolve) => {
                wait = resolve
              })
            }
            if (queue.length > 0) return { value: queue.shift(), done: false }
            return { value: undefined, done: true }
          },
        }
      },
    },
  }
}

const withFetchOk = async (fn) => {
  const real = globalThis.fetch
  globalThis.fetch = async () => ({ ok: true, json: async () => ({ healthy: true }) })
  try {
    return await fn()
  } finally {
    globalThis.fetch = real
  }
}

const disposeSubscription = (subscription) => {
  if (isNone(subscription)) return
  if (typeof subscription.Dispose === 'function') subscription.Dispose()
  else if (typeof subscription.dispose === 'function') subscription.dispose()
}

const subscribeGlobalWithVirtual = async (vt, streamCtl, onEvent = () => {}) => {
  const result = await withFetchOk(() =>
    hostSignalSubscribe.trySubscribe(
      {
        serverUrl: 'http://127.0.0.1:4096',
        client: {
          global: {
            event: async () => streamCtl.stream,
          },
        },
      },
      onEvent,
      vt.rawPort,
    ),
  )
  const decoded = resultOf(result)
  assert.equal(decoded.ok, true, decoded.error ?? 'subscribe ok')
  const [subscription, source] = decoded.value
  assert.equal(source, 'global.event')
  assert.ok(subscription && !isNone(subscription), 'global SSE subscription active')
  await settleAll()
  return subscription
}

test('VERIFY_004_virtual_timer_fires_exactly_when_advanced_past_deadline', async () => {
  const vt = timerPort.createVirtual()
  let fired = 0
  const handle = vt.port.delay(100)
  handle.delay().then(() => {
    fired += 1
  })

  vt.advance(99)
  await settle()
  assert.equal(fired, 0, 'not due before deadline')
  assert.equal(vt.nowMs(), 99)

  vt.advance(1)
  await settle()
  assert.equal(fired, 1, 'fires once at deadline')
  assert.equal(vt.nowMs(), 100)
})

test('VERIFY_004_virtual_timer_cancel_before_fire_yields_zero_callbacks', async () => {
  const vt = timerPort.createVirtual()
  let fired = 0
  const handle = vt.port.delay(50)
  handle.delay().then(() => {
    fired += 1
  })

  handle.cancel()
  vt.advance(1000)
  await settle()
  assert.equal(fired, 0, 'cancel must leave Delay pending forever')
})

test('VERIFY_004_virtual_timer_dispose_stops_all_pending_callbacks', async () => {
  const vt = timerPort.createVirtual()
  let fired = 0
  const a = vt.port.delay(10)
  const b = vt.port.delay(20)
  a.delay().then(() => {
    fired += 1
  })
  b.delay().then(() => {
    fired += 1
  })

  vt.port.dispose()
  vt.advance(1000)
  await settle()
  assert.equal(fired, 0, 'dispose clears pending entries without firing')
})

test('VERIFY_004_virtual_timer_multiple_handles_fire_independently', async () => {
  const vt = timerPort.createVirtual()
  const order = []
  const short = vt.port.delay(10)
  const long = vt.port.delay(30)
  short.delay().then(() => order.push('short'))
  long.delay().then(() => order.push('long'))

  vt.advance(10)
  await settle()
  assert.deepEqual(order, ['short'])

  vt.advance(20)
  await settle()
  assert.deepEqual(order, ['short', 'long'])
})

// ── Proof plan #4: SSE one-shot deadline under virtual ITimerPort ─────────────

test('HOST_signal_sse_continuous_events_zero_heartbeat_fatal', async () => {
  const vt = timerPort.createVirtual()
  const streamCtl = createControllableStream()
  const events = []
  const fatalLines = []
  const realError = console.error
  console.error = (line) => fatalLines.push(String(line))

  try {
    const subscription = await subscribeGlobalWithVirtual(vt, streamCtl, (ev) => events.push(ev))

    // Continuous pushes re-arm the one-shot deadline; advancing < timeout must not fatal.
    for (let i = 0; i < 5; i += 1) {
      streamCtl.push({ type: 'session.idle', i })
      await settleAll()
      vt.advance(HEARTBEAT_TIMEOUT_MS - 1)
      await settleAll()
    }
    assert.equal(fatalLines.length, 0, 'continuous events must not trip heartbeat fatal')
    assert.ok(events.length >= 5, `delivered events, got ${events.length}`)

    disposeSubscription(subscription)
    streamCtl.end()
    await settleAll()
  } finally {
    console.error = realError
  }
})

test('HOST_signal_sse_silence_threshold_fires_heartbeat_fatal_once', async () => {
  const vt = timerPort.createVirtual()
  const streamCtl = createControllableStream()
  const fatalLines = []
  const realError = console.error
  console.error = (line) => fatalLines.push(String(line))

  try {
    const subscription = await subscribeGlobalWithVirtual(vt, streamCtl)

    // Stop pushing; advance virtual clock to silence threshold (one-shot).
    vt.advance(HEARTBEAT_TIMEOUT_MS - 1)
    await settleAll()
    assert.equal(fatalLines.length, 0, 'just under threshold: no fatal')

    vt.advance(1)
    await settleAll()
    assert.equal(fatalLines.length, 1, 'exactly at threshold: one fatal')
    const payload = JSON.parse(fatalLines[0])
    assert.equal(payload.operation, 'sse-heartbeat-timeout')

    // Further advances must not re-fire (one-shot cancel + null).
    vt.advance(HEARTBEAT_TIMEOUT_MS)
    await settleAll()
    assert.equal(fatalLines.length, 1, 'one-shot: no second fatal')

    disposeSubscription(subscription)
    streamCtl.end()
    await settleAll()
  } finally {
    console.error = realError
  }
})

test('HOST_signal_sse_dispose_cancels_timer_zero_callbacks', async () => {
  const vt = timerPort.createVirtual()
  const streamCtl = createControllableStream()
  const fatalLines = []
  const realError = console.error
  console.error = (line) => fatalLines.push(String(line))

  try {
    const subscription = await subscribeGlobalWithVirtual(vt, streamCtl)

    disposeSubscription(subscription)
    streamCtl.end()
    await settleAll()

    vt.advance(HEARTBEAT_TIMEOUT_MS * 2)
    await settleAll()
    assert.equal(fatalLines.length, 0, 'dispose Cancel leaves heartbeat unfired')
  } finally {
    console.error = realError
  }
})

test('HOST_signal_sse_heartbeat_uses_ITimerPort_not_bare_setTimeout', () => {
  // Structure guard: production path must not reintroduce bare heartbeat timers.
  const src = hostSignalSubscribe.source()
  assert.ok(src.includes('port.Delay'), 'heartbeat arm via ITimerPort')
  assert.ok(src.includes('state.heartbeatHandle'), 'handle stored for Cancel')
  assert.ok(!src.includes('state.heartbeatTimer'), 'legacy setTimeout field removed')
  // probeServer may still use setTimeout for health probe (out of ITimerPort scope);
  // heartbeat/reconnect path must not arm via bare setTimeout for silence deadline.
  assert.ok(src.includes('onHeartbeatTimeout'), 'fatal path retained')
  // Reconnect markers retained.
  for (const marker of hostSignalSubscribe.reconnectMarkers) {
    assert.ok(src.includes(marker), `reconnect marker: ${marker}`)
  }
  // diagnostic import kept used (structure tests may not exercise fatal).
  assert.equal(typeof diagnostic.fatal, 'function')
})
