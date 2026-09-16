// requirements/process-execution/tests/large-gate.test.mjs
// Owner: process-execution.
//
// PROC-016: Large Gate 大输出单持有者互斥门禁与并发保护

import assert from 'node:assert/strict'
import test from 'node:test'

const {
  acquire,
  getCount,
  release,
  createToken,
  cancelToken,
  isCancellationRequested,
} = await import('../../../dist/Process/LargeGateSurface.js')

const live = () => createToken(false)
const cancelled = () => createToken(true)

const drain = () => {
  while (getCount() === 0) release()
}

const tick = () => new Promise((resolve) => setTimeout(resolve, 0))

test('WHAT[PROC-016] large_gate_first_acquire_succeeds_immediately', async () => {
  assert.equal(getCount(), 1, 'gate must start unheld')
  await acquire(live())
  assert.equal(getCount(), 0, 'holder makes the gate busy')
  drain()
})

test('WHAT[PROC-016] large_gate_second_acquire_waits_until_release', async () => {
  await acquire(live())
  let secondResolved = false
  const second = acquire(live()).then(() => {
    secondResolved = true
  })
  await tick()
  assert.equal(secondResolved, false, 'second acquire must wait behind the holder')

  release()
  await second
  assert.equal(secondResolved, true)
  drain()
})

test('WHAT[PROC-016] large_gate_release_without_holder_is_noop', async () => {
  release()
  assert.equal(getCount(), 1)
})

test('WHAT[PROC-016] large_gate_waiters_are_served_fifo', async () => {
  await acquire(live())
  let first = false
  let second = false
  const waiterA = acquire(live()).then(() => {
    first = true
  })
  const waiterB = acquire(live()).then(() => {
    second = true
  })
  await tick()
  assert.equal(first, false)
  assert.equal(second, false)

  release() // grants A
  await waiterA
  assert.equal(first, true)
  assert.equal(second, false, 'B must stay queued behind A')

  release() // grants B
  await waiterB
  assert.equal(second, true)
  drain()
})

test('WHAT[PROC-016] large_gate_cancelled_waiter_is_skipped', async () => {
  await acquire(live())
  const token = live()
  const waiter = acquire(token)
  cancelToken(token)
  await assert.rejects(waiter, 'a cancelled waiter must reject, not block the queue')

  release()
  assert.equal(getCount(), 1, 'cancelled waiter must not consume the permit')
  drain()
})

test('WHAT[PROC-016] large_gate_precancelled_token_is_rejected_immediately', async () => {
  await assert.rejects(acquire(cancelled()))
  assert.equal(getCount(), 1, 'a refused acquire must not hold the gate')
})

test('WHAT[PROC-016] large_gate_cancellation_observed_by_gate', async () => {
  const token = live()
  assert.equal(isCancellationRequested(token), false)
  cancelToken(token)
  assert.equal(isCancellationRequested(token), true)
})

test('WHAT[PROC-016] large_gate_acquire_after_release_reenters_cleanly', async () => {
  await acquire(live())
  release()
  assert.equal(getCount(), 1)
  await acquire(live())
  assert.equal(getCount(), 0)
  drain()
})
