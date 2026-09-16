// TIME-001/003/005 — explicit virtual clocks and injected deadline views.

import assert from 'node:assert/strict'
import test from 'node:test'

const process = await import('../../../dist/Process/Surface.js')
const deadline = await import('../../../dist/Process/DeadlineSurface.js')

const START_MS = Date.parse('2000-01-01T00:00:00Z')

test('WHAT[TIME-003] TIME_003_virtual_clock_starts_at_fixed_epoch', () => {
  const clock = process.createVirtualClock()
  assert.equal(Number(process.clockNowMs(clock)), START_MS)
})

test('WHAT[TIME-003] TIME_003_virtual_clock_advance_and_set_are_deterministic', () => {
  const clock = process.createVirtualClock()

  process.clockAdvanceMs(clock, 5000)
  assert.equal(Number(process.clockNowMs(clock)), START_MS + 5000)

  process.clockAdvanceMs(clock, 0)
  assert.equal(Number(process.clockNowMs(clock)), START_MS + 5000, 'zero advance must not move the clock')

  process.clockSet(clock, '2026-01-01T00:00:00Z')
  assert.equal(Number(process.clockNowMs(clock)), Date.parse('2026-01-01T00:00:00Z'))
})

test('WHAT[TIME-001] TIME_001_virtual_clocks_are_independent_not_ambient', () => {
  const first = process.createVirtualClock()
  const second = process.createVirtualClock()

  process.clockAdvanceMs(first, 10_000)
  assert.equal(Number(process.clockNowMs(first)), START_MS + 10_000)
  assert.equal(Number(process.clockNowMs(second)), START_MS, 'advancing one clock must not move another')
})

test('WHAT[TIME-005] TIME_005_deadline_verdict_uses_injected_clock_view', () => {
  const clock = process.createVirtualClock()
  process.clockSet(clock, '2026-01-01T00:00:00Z')
  const value = deadline.create('2026-01-01T00:00:00Z', 5000)

  assert.equal(deadline.isExpired(process.clockNowIso(clock), value), false)
  process.clockAdvanceMs(clock, 6000)
  assert.equal(deadline.isExpired(process.clockNowIso(clock), value), true)
  assert.equal(deadline.remainingMs(process.clockNowIso(clock), value), 0)
})
