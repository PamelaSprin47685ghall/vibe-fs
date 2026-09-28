import assert from 'node:assert/strict'
import test from 'node:test'
import * as temporal from '../../../dist/Process/Surface.js'
import * as deadline from '../../../dist/Process/DeadlineSurface.js'

test('WHAT[time-capability-005] the same deadline follows the supplied clock without changing another clock', () => {
  const first = temporal.createVirtualClock()
  const second = temporal.createVirtualClock()
  const start = '2026-01-01T00:00:00Z'
  temporal.clockSet(first, start)
  temporal.clockSet(second, start)
  const value = deadline.create(start, 5000)
  temporal.clockAdvanceMs(first, 6000)
  assert.equal(deadline.isExpired(temporal.clockNowIso(first), value), true)
  assert.equal(deadline.remainingMs(temporal.clockNowIso(first), value), 0)
  assert.equal(deadline.isExpired(temporal.clockNowIso(second), value), false)
  assert.equal(deadline.remainingMs(temporal.clockNowIso(second), value), 5000)
  temporal.clockSet(first, start)
  assert.equal(deadline.isExpired(temporal.clockNowIso(first), value), false)
})
