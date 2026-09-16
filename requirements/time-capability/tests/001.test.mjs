// TIME-001/003/005 — explicit virtual clocks and injected deadline views.

import assert from 'node:assert/strict'
import test from 'node:test'

const process = await import('../../../dist/Process/Surface.js')
const deadline = await import('../../../dist/Process/DeadlineSurface.js')

const START_MS = Date.parse('2000-01-01T00:00:00Z')

test('WHAT[TIME-001] TIME_001_virtual_clocks_are_independent_not_ambient', () => {
  const first = process.createVirtualClock()
  const second = process.createVirtualClock()

  process.clockAdvanceMs(first, 10_000)
  assert.equal(Number(process.clockNowMs(first)), START_MS + 10_000)
  assert.equal(Number(process.clockNowMs(second)), START_MS, 'advancing one clock must not move another')
})
