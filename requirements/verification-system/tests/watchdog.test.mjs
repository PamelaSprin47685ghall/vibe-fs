// requirements/verification-system/tests/watchdog.test.mjs
//
// Virtual-time driven tests for Watchdog (VERIFY-004).
// Driven deterministically via createVirtualClock without physical sleeps.

import assert from 'node:assert/strict'
import test from 'node:test'

import { createVirtualClock } from './support/temporal-harness.mjs'
import { Watchdog } from './e2e/support/watchdog.js'
import { WATCHDOG_TIMEOUT_MS } from './e2e/support/time-budget.js'

/**
 * Adapter converting createVirtualClock's delay()/cancel() to Watchdog's timers port interface:
 * timers.schedule(ms, cb) -> id
 * timers.cancel(id)
 */
function createVirtualTimers(clock) {
  const handles = new Map()
  let nextId = 1
  return {
    schedule(ms, cb) {
      const id = nextId++
      const h = clock.port.delay(ms)
      let active = true
      handles.set(id, {
        cancel: () => {
          active = false
          h.cancel()
        },
      })
      h.delay().then(() => {
        if (active) {
          handles.delete(id)
          cb()
        }
      })
      return id
    },
    cancel(id) {
      const entry = handles.get(id)
      if (entry) {
        handles.delete(id)
        entry.cancel()
      }
    },
  }
}

function createHarness(opts = {}) {
  const clock = createVirtualClock()
  const timers = createVirtualTimers(clock)
  const diagnostics = []
  let terminated = false
  let terminateCount = 0

  const deps = {
    clock: { nowMs: () => clock.nowMs() },
    timers,
    diagnostic: {
      write: (msg) => {
        diagnostics.push(msg)
      },
    },
    terminate: () => {
      terminated = true
      terminateCount += 1
    },
  }

  const watchdog = new Watchdog({
    timeoutMs: opts.timeoutMs ?? 1000,
    label: opts.label ?? 'test-dog',
    onTimeout: opts.onTimeout,
    deps,
  })

  return {
    clock,
    timers,
    diagnostics,
    watchdog,
    get terminated() {
      return terminated
    },
    get terminateCount() {
      return terminateCount
    },
    async advance(ms, flushRounds = 3) {
      clock.advance(ms)
      for (let r = 0; r < flushRounds; r += 1) await Promise.resolve()
      await Promise.resolve()
    },
  }
}

test('WHAT[VERIFICATION-SYSTEM-004] watchdog fires on silence after exact timeoutMs', async () => {
  const h = createHarness({ timeoutMs: 500, label: 'dog-silence' })

  await h.advance(499)
  assert.equal(h.terminated, false, 'must not fire before timeout')
  assert.equal(h.diagnostics.length, 0)

  await h.advance(1)
  assert.equal(h.terminated, true, 'must terminate at timeout')
  assert.equal(h.terminateCount, 1)
  assert.ok(h.diagnostics.some((d) => d.includes("WATCHDOG: 'dog-silence' silent for 500ms")))
})

test('WHAT[VERIFICATION-SYSTEM-004] blocking renew pushes timeout forward indefinitely', async () => {
  const h = createHarness({ timeoutMs: 300, label: 'dog-renew' })

  await h.advance(200)
  assert.equal(h.terminated, false)
  h.watchdog.advance({ reason: 'step-1', lane: 'worker', blocking: true })

  // Another 200ms passed; total 400ms > initial 300ms, but reset at 200ms
  await h.advance(200)
  assert.equal(h.terminated, false)
  h.watchdog.advance({ reason: 'step-2', lane: 'worker', blocking: true })

  await h.advance(250)
  assert.equal(h.terminated, false)

  // Now let it stay silent for 300ms from last progress
  await h.advance(50)
  assert.equal(h.terminated, true)
  assert.ok(h.diagnostics.some((d) => d.includes('2 blocking progress update(s)')))
  assert.ok(h.diagnostics.some((d) => d.includes('last progress: step-2 lane=worker')))
})

test('WHAT[VERIFICATION-SYSTEM-004] background advance records info but does not renew timeout', async () => {
  const h = createHarness({ timeoutMs: 400, label: 'dog-background' })

  await h.advance(100)
  h.watchdog.advance({ reason: 'bg-step', lane: 'sidecar', blocking: false })
  assert.equal(h.terminated, false)

  await h.advance(200)
  h.watchdog.advance({ reason: 'bg-step-2', lane: 'sidecar', blocking: false })
  assert.equal(h.terminated, false)

  // Total elapsed 400ms: background advances did not push timeoutMs
  await h.advance(100)
  assert.equal(h.terminated, true)
  assert.ok(h.diagnostics.some((d) => d.includes('0 blocking progress update(s)')))
  assert.ok(h.diagnostics.some((d) => d.includes('background progress 100ms ago: bg-step-2 lane=sidecar (2 background update(s), none of them renewals)')))
})

test('WHAT[VERIFICATION-SYSTEM-004] stop permanently disarms watchdog with no subsequent fire', async () => {
  const h = createHarness({ timeoutMs: 200, label: 'dog-stopped' })

  await h.advance(100)
  h.watchdog.stop()

  await h.advance(500)
  assert.equal(h.terminated, false, 'stopped watchdog must never terminate')
  assert.equal(h.diagnostics.length, 0)

  // advance and setWindow after stop must be safe no-ops
  h.watchdog.advance({ reason: 'post-stop', lane: 'worker', blocking: true })
  h.watchdog.setWindow(50)
  await h.advance(500)
  assert.equal(h.terminated, false)
})

test('WHAT[VERIFICATION-SYSTEM-004] setWindow(null) restores centralized default WATCHDOG_TIMEOUT_MS', async () => {
  const h = createHarness({ timeoutMs: 500, label: 'dog-window' })

  // Widen to 2000ms
  h.watchdog.setWindow(2000)
  await h.advance(1000)
  assert.equal(h.terminated, false, 'widened window allows 1000ms')

  // setWindow(null) restores WATCHDOG_TIMEOUT_MS (e.g. 5000ms)
  h.watchdog.setWindow(null)
  assert.equal(h.watchdog._timeoutMs, WATCHDOG_TIMEOUT_MS)

  // Advance by WATCHDOG_TIMEOUT_MS - 1
  await h.advance(WATCHDOG_TIMEOUT_MS - 1)
  assert.equal(h.terminated, false)

  // 1ms more fires at the centralized default limit
  await h.advance(1)
  assert.equal(h.terminated, true)
  assert.ok(h.diagnostics.some((d) => d.includes(`(limit ${WATCHDOG_TIMEOUT_MS}ms)`)))
})

test('WHAT[VERIFICATION-SYSTEM-004] timeout fires at most once even if clock continues to advance', async () => {
  const h = createHarness({ timeoutMs: 300, label: 'dog-once' })

  await h.advance(300)
  assert.equal(h.terminateCount, 1)

  await h.advance(1000)
  assert.equal(h.terminateCount, 1, 'terminate must not be called repeatedly')
})

test('WHAT[VERIFICATION-SYSTEM-004] diagnostic is flushed before terminate and onTimeout is bounded', async () => {
  const executionOrder = []
  const clock = createVirtualClock()
  const timers = createVirtualTimers(clock)

  let onTimeoutResolved = false
  const onTimeout = async () => {
    executionOrder.push('onTimeout:start')
    onTimeoutResolved = true
    executionOrder.push('onTimeout:done')
  }

  const deps = {
    clock: { nowMs: () => clock.nowMs() },
    timers,
    diagnostic: {
      write: (msg) => {
        executionOrder.push(`diagnostic:${msg.slice(0, 8)}`)
      },
    },
    terminate: () => {
      executionOrder.push('terminate')
    },
  }

  const watchdog = new Watchdog({
    timeoutMs: 200,
    label: 'dog-order',
    onTimeout,
    deps,
  })

  clock.advance(200)
  // Flush microtask ticks through Promise.race and onTimeout async completion
  for (let i = 0; i < 5; i += 1) await Promise.resolve()
  await Promise.resolve()

  assert.equal(onTimeoutResolved, true)
  assert.deepEqual(executionOrder, [
    'diagnostic:WATCHDOG',
    'onTimeout:start',
    'onTimeout:done',
    'terminate',
  ])
})
