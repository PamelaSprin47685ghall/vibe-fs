import assert from 'node:assert/strict'
import test from 'node:test'
import * as relay from '../../../dist/Mission/Relay/Surface.js'

const open = (state, road = 'road-1', incumbent = 'inc-1') =>
  relay.openIncumbency(state, road, incumbent, 'snapshot-1', 'authority-1')

test('WHAT[relay-incumbency-004] low-score assessor takes work ownership in place without a new iteration', () => {
  const first = open(relay.empty())
  const assessed = relay.assess(
    first.state,
    'road-1',
    'inc-1',
    'assessment-1',
    'snapshot-1',
    'authority-1',
    'REVISE', 'PERFECT', 'PERFECT', 'PERFECT', 'PERFECT', 'PERFECT', 'PERFECT', 'PERFECT',
  )
  assert.equal(assessed.ok, true)
  assert.deepEqual(relay.view(assessed.state, 'road-1'), {
    activeIncumbency: 'inc-1',
    iterationOrdinal: 1,
    phase: 'WorkOwned',
    retired: [],
  })
})
