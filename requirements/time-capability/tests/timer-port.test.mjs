// tests/unit/execution/timer-port.test.mjs — ITimerPort contract (VERIFY-004).

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  timerPort,
} from '../../../tests/unit/support/domain.mjs'

const settle = () => new Promise((r) => setImmediate(r))

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

