import assert from 'node:assert/strict'
import test from 'node:test'
import * as fission from '../../../dist/Execution/Fission/Surface.js'

test('WHAT[intra-participant-parallelism-014] production settlement decision yields nonfinal observations to the ordinary turn owner', () => {
  for (const phase of ['lane', 'takeover']) {
    for (const observation of ['running', 'needs-continuation', 'provider-failed', 'degeneration-interrupted']) {
      assert.equal(fission.settlementDecision(phase, observation), 'yield-to-turn-workflow')
    }
    assert.equal(fission.settlementDecision(phase, 'external-abort'), 'fail-group')
  }
  assert.equal(fission.settlementDecision('lane', 'completed'), 'materialize-lane')
  assert.equal(fission.settlementDecision('takeover', 'completed'), 'complete-owner')
})

test.todo('WHAT[intra-participant-parallelism-014] actual lane and takeover continuation completes before settlement, including nudge, AABB and degeneration abort through their real owners (GAP-158)')
