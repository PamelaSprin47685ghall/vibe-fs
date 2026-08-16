// requirements/time-capability/tests/deadline-typed.test.mjs
// TIME-002 / TIME-005 — typed Deadline: bounded, overflow-clamped, JS-timer
// ceiling segmented waits, and verdicts that follow injected clock input.

import assert from 'node:assert/strict'
import test from 'node:test'
import { assertOpaque } from '../../verification-system/tests/support/js-contract.mjs'

const deadline = await import('../../../dist/Process/DeadlineSurface.js')
const ISO_START = '2026-01-01T00:00:00Z'

test('WHAT[TIME-002] TIME_002_deadline_of_budget_and_remaining_are_pure_clock_functions', () => {
  const dl = deadline.create(ISO_START, 5000)
  assertOpaque(dl, 'deadline')

  assert.equal(deadline.remainingMs('2026-01-01T00:00:02Z', dl), 3000)
  assert.equal(deadline.isExpired('2026-01-01T00:00:02Z', dl), false)
  assert.equal(deadline.isExpired('2026-01-01T00:00:05Z', dl), true)
  assert.equal(deadline.remainingMs('2026-01-01T00:00:06Z', dl), 0)
})

test('WHAT[TIME-002] TIME_002_of_budget_clamps_to_datetime_max_no_overflow', () => {
  const dl = deadline.create(ISO_START, 1e15)
  assert.equal(deadline.isExpired('2099-01-01T00:00:00Z', dl), false)
  const remainingAtStart = deadline.remainingMs(ISO_START, dl)
  assert.ok(Number.isFinite(remainingAtStart))
  assert.ok(remainingAtStart > 0)
})

test('WHAT[TIME-002] TIME_002_next_wait_ms_caps_at_js_timer_ceiling', () => {
  assert.equal(deadline.maxTimerWaitMs, 2147483647)

  const long = deadline.create(ISO_START, 1e15)
  assert.equal(deadline.nextWaitMs(ISO_START, long), 2147483647)

  const short = deadline.create(ISO_START, 5000)
  assert.equal(deadline.nextWaitMs('2026-01-01T00:00:01Z', short), 4000)
  assert.equal(deadline.nextWaitMs('2026-01-01T00:00:06Z', short), 0)
})

test('WHAT[TIME-005] TIME_005_verdict_follows_injected_clock_not_value', () => {
  const dl = deadline.create(ISO_START, 5000)
  assert.equal(deadline.isExpired('2026-01-01T00:00:04Z', dl), false)
  assert.equal(deadline.isExpired('2026-01-01T00:00:06Z', dl), true)
  assert.equal(deadline.remainingMs('2026-01-01T00:00:02Z', dl), 3000)
  assert.equal(deadline.remainingMs('2026-01-01T00:00:08Z', dl), 0)

  assert.equal(deadline.isExpired('2026-01-01T00:00:10Z', dl), true)
  assert.equal(deadline.isExpired('2026-01-01T00:00:01Z', dl), false)
})
