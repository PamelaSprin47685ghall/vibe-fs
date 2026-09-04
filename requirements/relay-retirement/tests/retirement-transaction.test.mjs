import assert from 'node:assert/strict'
import test from 'node:test'
import * as relay from '../../../dist/Mission/Relay/Surface.js'

test('WHAT[RETIRE-007] retirement commits retired baton cut and successor request as one fold-visible state transition', () => {
  const opened = relay.openIncumbency(relay.empty(), 'road-1', 'inc-1', 'snapshot-1', 'authority-1', 'ExistingWorld')
  const retired = relay.retire(opened.state, 'road-1', 'inc-1', 'ret-1', 'snapshot-2', 'baton-1', 'cut-1', false)
  assert.equal(retired.ok, true)
  assert.deepEqual(relay.retirement(retired.state, 'road-1'), {
    retirementId: 'ret-1',
    incumbentId: 'inc-1',
    snapshotId: 'snapshot-2',
    batonId: 'baton-1',
    projectionCutId: 'cut-1',
    successorRequested: true,
    qualityCandidateAccepted: false,
  })
  assert.equal(relay.view(retired.state, 'road-1').activeIncumbency, null)
})

