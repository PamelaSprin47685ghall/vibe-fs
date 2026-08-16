// TIME-003 — virtual timers fire only when explicitly advanced.

import assert from 'node:assert/strict'
import test from 'node:test'
import { assertOpaque } from '../../verification-system/tests/support/js-contract.mjs'

const process = await import('../../../dist/Process/Surface.js')
const settle = () => new Promise((resolve) => setImmediate(resolve))

test('WHAT[TIME-003] VERIFY_004_virtual_timer_fires_exactly_when_advanced_past_deadline', async () => {
  const timer = process.createVirtualTimer()
  const handle = process.timerDelay(timer, 100)
  assertOpaque(timer, 'virtual timer')
  assertOpaque(handle, 'deadline handle')
  let fired = 0
  process.timerAwait(handle).then(() => {
    fired += 1
  })

  process.timerAdvance(timer, 99)
  await settle()
  assert.equal(fired, 0, 'not due before deadline')
  assert.equal(process.timerNowMs(timer), 99)

  process.timerAdvance(timer, 1)
  await settle()
  assert.equal(fired, 1, 'fires once at deadline')
  assert.equal(process.timerNowMs(timer), 100)
  process.timerDispose(timer)
})

test('WHAT[TIME-003] VERIFY_004_virtual_timer_cancel_before_fire_yields_zero_callbacks', async () => {
  const timer = process.createVirtualTimer()
  const handle = process.timerDelay(timer, 50)
  let fired = 0
  process.timerAwait(handle).then(() => {
    fired += 1
  })

  process.timerCancel(handle)
  process.timerAdvance(timer, 1000)
  await settle()
  assert.equal(fired, 0, 'cancel must leave Delay pending forever')
  process.timerDispose(timer)
})

test('WHAT[TIME-003] VERIFY_004_virtual_timer_dispose_stops_all_pending_callbacks', async () => {
  const timer = process.createVirtualTimer()
  const first = process.timerDelay(timer, 10)
  const second = process.timerDelay(timer, 20)
  let fired = 0
  process.timerAwait(first).then(() => {
    fired += 1
  })
  process.timerAwait(second).then(() => {
    fired += 1
  })

  process.timerDispose(timer)
  process.timerAdvance(timer, 1000)
  await settle()
  assert.equal(fired, 0, 'dispose clears pending entries without firing')
})

test('WHAT[TIME-003] VERIFY_004_virtual_timer_multiple_handles_fire_independently', async () => {
  const timer = process.createVirtualTimer()
  const order = []
  const short = process.timerDelay(timer, 10)
  const long = process.timerDelay(timer, 30)
  process.timerAwait(short).then(() => order.push('short'))
  process.timerAwait(long).then(() => order.push('long'))

  process.timerAdvance(timer, 10)
  await settle()
  assert.deepEqual(order, ['short'])

  process.timerAdvance(timer, 20)
  await settle()
  assert.deepEqual(order, ['short', 'long'])
  process.timerDispose(timer)
})
