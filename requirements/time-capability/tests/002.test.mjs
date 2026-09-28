import assert from 'node:assert/strict'
import test from 'node:test'
import { assertOpaque } from '../../verification-system/tests/support/js-contract.mjs'
import * as deadline from '../../../dist/Process/DeadlineSurface.js'

const START = '2026-01-01T00:00:00Z'

test('WHAT[time-capability-002] an opaque deadline is decided by explicit instants including the exact boundary', () => {
  const value = deadline.create(START, 5000)
  assertOpaque(value, 'deadline')
  assert.equal(deadline.remainingMs('2026-01-01T00:00:02Z', value), 3000)
  assert.equal(deadline.isExpired('2026-01-01T00:00:04.999Z', value), false)
  assert.equal(deadline.isExpired('2026-01-01T00:00:05Z', value), true)
  assert.equal(deadline.remainingMs('2026-01-01T00:00:06Z', value), 0)
  assert.equal(deadline.remainingMs('2026-01-01T08:00:02+08:00', value), 3000)
  assert.equal(deadline.isExpired('2026-01-01T08:00:05+08:00', value), true)
  assert.equal(deadline.isExpired(START, deadline.create(START, 0)), true)
})

test('WHAT[time-capability-002] a huge budget remains finite and waits are segmented at the physical timer limit', () => {
  const value = deadline.create(START, 1e15)
  assert.equal(deadline.isExpired('2099-01-01T00:00:00Z', value), false)
  assert.ok(Number.isFinite(deadline.remainingMs(START, value)))
  assert.ok(deadline.remainingMs(START, value) > 0)
  assert.equal(deadline.maxTimerWaitMs, 2147483647)
  assert.equal(deadline.nextWaitMs(START, value), deadline.maxTimerWaitMs)
  const short = deadline.create(START, 5000)
  assert.equal(deadline.nextWaitMs('2026-01-01T00:00:01Z', short), 4000)
  assert.equal(deadline.nextWaitMs('2026-01-01T00:00:06Z', short), 0)
})
