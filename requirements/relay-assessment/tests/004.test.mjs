import assert from 'node:assert/strict'
import test from 'node:test'
import * as relay from '../../../dist/Mission/Relay/Surface.js'

const scores = ['PERFECT', 'REVISE', 'PERFECT', 'REVISE', 'PERFECT', 'PERFECT', 'REVISE', 'PERFECT']

const open = (state, snapshot = 'snapshot-1') =>
  relay.openIncumbency(state, 'road-1', 'inc-1', snapshot, 'authority-1')

test('WHAT[relay-assessment-004] revise assessment atomically records obligations and grants work ownership', () => {
  const opened = open(relay.empty())
  const assessed = relay.assess(opened.state, 'road-1', 'inc-1', 'assessment-1', 'snapshot-1', 'authority-1', ...scores)
  assert.equal(assessed.ok, true)
  assert.deepEqual(relay.view(assessed.state, 'road-1'), {
    activeIncumbency: 'inc-1',
    iterationOrdinal: 1,
    phase: 'WorkOwned',
    retired: [],
  })
  assert.equal(relay.certificate(assessed.state, 'road-1'), null)
})
