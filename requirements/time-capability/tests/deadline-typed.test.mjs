// requirements/time-capability/tests/deadline-typed.test.mjs
// TIME-002 / TIME-005 — typed Deadline: bounded, overflow-clamped, JS-timer-ceiling
// segmented waits, and verdicts that follow the injected clock, never the value.

import assert from 'node:assert/strict'
import test from 'node:test'
import { clockAt, deadline } from '../../verification-system/tests/support/domain.mjs'

const ISO_START = '2026-01-01T00:00:00Z'

test('WHAT[TIME-002] TIME_002_deadline_of_budget_and_remaining_are_pure_clock_functions', () => {
  const dl = deadline.ofBudget(ISO_START, 5000)

  assert.equal(deadline.remainingMs(clockAt('2026-01-01T00:00:02Z'), dl), 3000)
  assert.equal(deadline.isExpired(clockAt('2026-01-01T00:00:02Z'), dl), false)
  assert.equal(deadline.isExpired(clockAt('2026-01-01T00:00:05Z'), dl), true)
  // remaining clamps at zero — never negative.
  assert.equal(deadline.remainingMs(clockAt('2026-01-01T00:00:06Z'), dl), 0)
})

test('WHAT[TIME-002] TIME_002_of_budget_clamps_to_datetime_max_no_overflow', () => {
  // A ~31k-year budget exceeds the remaining lifetime until DateTimeOffset.MaxValue;
  // the deadline must stay computable, not NaN / negative / wildly wrong.
  const dl = deadline.ofBudget(ISO_START, 1e15)

  assert.equal(deadline.isExpired(clockAt('2099-01-01T00:00:00Z'), dl), false)
  const remainingAtStart = deadline.remainingMs(clockAt(ISO_START), dl)
  assert.ok(Number.isFinite(remainingAtStart))
  assert.ok(remainingAtStart > 0)
})

test('WHAT[TIME-002] TIME_002_next_wait_ms_caps_at_js_timer_ceiling', () => {
  assert.equal(deadline.maxTimerWaitMs, 2147483647)

  // Huge legal estimate → the caller waits in segments at the JS timer ceiling.
  const long = deadline.ofBudget(ISO_START, 1e15)
  assert.equal(deadline.nextWaitMs(clockAt(ISO_START), long), 2147483647)

  // Within the ceiling → exact remaining; expired → 0.
  const short = deadline.ofBudget(ISO_START, 5000)
  assert.equal(deadline.nextWaitMs(clockAt('2026-01-01T00:00:01Z'), short), 4000)
  assert.equal(deadline.nextWaitMs(clockAt('2026-01-01T00:00:06Z'), short), 0)
})

test('WHAT[TIME-005] TIME_005_verdict_follows_injected_clock_not_value', () => {
  const dl = deadline.ofBudget(ISO_START, 5000)

  // The same typed value, consumed by the same rules under two injected clocks,
  // gives two different verdicts: the Deadline carries no authority of its own —
  // the consuming rule + the injected clock decide.
  assert.equal(deadline.isExpired(clockAt('2026-01-01T00:00:04Z'), dl), false)
  assert.equal(deadline.isExpired(clockAt('2026-01-01T00:00:06Z'), dl), true)
  assert.equal(deadline.remainingMs(clockAt('2026-01-01T00:00:02Z'), dl), 3000)
  assert.equal(deadline.remainingMs(clockAt('2026-01-01T00:00:08Z'), dl), 0)

  // Two independent clocks disagree about the same value — time is per-consumer input.
  const fast = clockAt('2026-01-01T00:00:10Z')
  const slow = clockAt('2026-01-01T00:00:01Z')
  assert.equal(deadline.isExpired(fast, dl), true)
  assert.equal(deadline.isExpired(slow, dl), false)
})
