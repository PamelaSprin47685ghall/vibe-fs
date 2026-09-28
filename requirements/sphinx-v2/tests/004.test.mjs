import assert from 'node:assert/strict'
import test from 'node:test'
import * as Loop from '../../../dist/Sphinx/V2/Runtime/Surface.js'

test('WHAT[sphinx-v2-004] provider receipt classification preserves unresolved usage rather than billing it as zero', () => {
  assert.equal(Loop.providerUsageUnresolved(Loop.providerOutcome('', 0n, 0n, 0n, true)), true)
  const settled = Loop.providerOutcome('', 100n, 200n, 3n, false)
  assert.equal(Loop.providerUsageUnresolved(settled), false)
  assert.deepEqual(Loop.providerUsageCounts(settled), [100n, 200n, 3n])
})

test.todo('WHAT[sphinx-v2-004] actual durable reservations survive unresolved usage and settle cancellation duplicate calls and observed overrun exactly once')
