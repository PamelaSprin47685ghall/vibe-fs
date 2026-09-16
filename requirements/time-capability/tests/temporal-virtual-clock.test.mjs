// TIME-005 — virtual time is input, never authority.

import assert from 'node:assert/strict'
import test from 'node:test'

const process = await import('../../../dist/Process/Surface.js')
const settle = () => new Promise((resolve) => setImmediate(resolve))

test('WHAT[TIME-005] TEMPORAL_virtual_clock_time_is_input_not_authority', async () => {
  const timer = process.createVirtualTimer()
  let fired = 0
  const handle = process.timerDelay(timer, 100)
  process.timerAwait(handle).then(() => {
    fired += 1
  })
  assert.equal(fired, 0, 'must not fire before advance')
  process.timerAdvance(timer, 99)
  await settle()
  assert.equal(fired, 0, '99ms of 100ms deadline must not fire')
  process.timerAdvance(timer, 1)
  await process.timerAwait(handle)
  assert.equal(fired, 1, 'advance past deadline fires exactly once')
  process.timerDispose(timer)
})

test('WHAT[TIME-005] TEMPORAL_virtual_clock_cancel_and_dispose_yield_zero_callbacks', async () => {
  const timer = process.createVirtualTimer()
  let fired = 0
  const first = process.timerDelay(timer, 10)
  const second = process.timerDelay(timer, 20)
  process.timerAwait(first).then(() => {
    fired += 1
  })
  process.timerAwait(second).then(() => {
    fired += 1
  })
  process.timerCancel(first)
  process.timerAdvance(timer, 30)
  await settle()
  assert.equal(fired, 1, 'cancelled handle must not fire; other handle fires once')

  process.timerDispose(timer)
  const afterDispose = process.timerDelay(timer, 10)
  let firedAfterDispose = 0
  process.timerAwait(afterDispose).then(() => {
    firedAfterDispose += 1
  })
  process.timerAdvance(timer, 10)
  await settle()
  assert.equal(firedAfterDispose, 0)
})
