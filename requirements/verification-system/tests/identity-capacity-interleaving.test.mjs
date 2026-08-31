import assert from 'node:assert/strict'
import test from 'node:test'

import {
  defaultFamily,
  invalidCausalEdge,
  operations,
  permutations,
  prerequisites,
  runInterleaving,
  validPermutations,
} from './support/identity-capacity-interleaving.mjs'

test('WHAT[VERIFICATION-SYSTEM-007] executes every valid identity/admission/capacity causal interleaving', async () => {
  assert.deepEqual(operations, [
    'parent accepted',
    'child dispatched',
    'child returns',
    'parent new prompt',
    'child terminal',
    'capacity release',
  ])
  assert.deepEqual(prerequisites, {
    'parent accepted': [],
    'child dispatched': ['parent accepted'],
    'child returns': ['child dispatched'],
    'parent new prompt': ['child dispatched'],
    'child terminal': ['child dispatched'],
    'capacity release': ['child terminal'],
  })
  assert.equal(permutations.length, 720)
  assert.equal(validPermutations.length, 12)

  const observations = []
  for (const schedule of validPermutations) {
    observations.push(await runInterleaving(schedule, defaultFamily))
  }

  assert.equal(observations.length, 12)
  assert.equal(new Set(observations.map(({ schedule }) => schedule.join(' → '))).size, 12)
  assert.ok(observations.every(({ providerDispatches }) => providerDispatches === 1))
})

test('WHAT[VERIFICATION-SYSTEM-007] rejects every causally invalid ordering before effects', async () => {
  const invalid = permutations.filter((schedule) => invalidCausalEdge(schedule) !== null)
  assert.equal(invalid.length, 708)

  for (const schedule of invalid) {
    const edge = invalidCausalEdge(schedule)
    assert.ok(edge)
    await assert.rejects(runInterleaving(schedule, defaultFamily), (error) => {
      assert.equal(error.code, 'INVALID_CAUSAL_ORDER')
      assert.equal(error.operation, edge.operation)
      assert.equal(error.missing, edge.missing)
      return true
    })
  }
})
