// requirements/time-capability/tests/clock-port-virtual.test.mjs
// TIME-001 / TIME-003 / TIME-005 — IClockPort virtual clock: deterministic start,
// advance/set, per-consumer independence (no ambient).

import assert from 'node:assert/strict'
import test from 'node:test'
import { clockAt, clockPort, deadline, utcOffset } from '../../verification-system/tests/support/domain.mjs'

const START_MS = Date.parse('2000-01-01T00:00:00Z')

test('WHAT[TIME-003] TIME_003_virtual_clock_starts_at_fixed_epoch', () => {
  const vc = clockPort.createVirtual()
  assert.equal(vc.utcNow().getTime(), START_MS)
})

test('WHAT[TIME-003] TIME_003_virtual_clock_advance_and_set_are_deterministic', () => {
  const vc = clockPort.createVirtual()

  vc.advanceMs(5000)
  assert.equal(vc.utcNow().getTime(), START_MS + 5000)

  vc.advanceMs(0)
  assert.equal(vc.utcNow().getTime(), START_MS + 5000, 'zero advance must not move the clock')

  vc.set(utcOffset('2026-01-01T00:00:00Z'))
  assert.equal(vc.utcNow().getTime(), Date.parse('2026-01-01T00:00:00Z'))
})

test('WHAT[TIME-001] TIME_001_virtual_clocks_are_independent_not_ambient', () => {
  const a = clockPort.createVirtual()
  const b = clockPort.createVirtual()

  a.advanceMs(10_000)
  assert.equal(a.utcNow().getTime(), START_MS + 10_000)
  assert.equal(b.utcNow().getTime(), START_MS, 'advancing one clock must not move another')
})

test('WHAT[TIME-005] TIME_005_deadline_verdict_uses_injected_clock_view', () => {
  const vc = clockPort.createVirtual()
  vc.set(utcOffset('2026-01-01T00:00:00Z'))
  const dl = deadline.ofBudget('2026-01-01T00:00:00Z', 5000)

  // The deadline is judged through the injected clock thunk, not an ambient now.
  assert.equal(deadline.isExpired(vc.utcNow, dl), false)
  vc.advanceMs(6000)
  assert.equal(deadline.isExpired(vc.utcNow, dl), true)
  assert.equal(deadline.remainingMs(vc.utcNow, dl), 0)
})
