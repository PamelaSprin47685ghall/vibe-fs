import assert from 'node:assert/strict'
import test from 'node:test'
import * as fission from '../../../dist/Execution/Fission/Surface.js'

test('WHAT[intra-participant-parallelism-014] production settlement decision yields nonfinal observations to the ordinary turn owner', () => {
  for (const phase of ['lane', 'takeover']) {
    for (const observation of ['running', 'needs-continuation', 'provider-failed']) {
      assert.equal(fission.settlementDecision(phase, observation), 'yield-to-turn-workflow')
    }
    assert.equal(fission.settlementDecision(phase, 'external-abort'), 'fail-group')
  }
  assert.equal(fission.settlementDecision('lane', 'completed'), 'materialize-lane')
  // LC-011: the pure decision still routes a stable completed takeover to
  // complete-owner, but the actual owner terminal publication is gated by
  // FissionHost.publishOwnerTakeover on an independent durable TaskDelivery
  // witness — a takeover owner that stops without its own integrated
  // delivery is rejected (RejectedUndelivered), never published as complete.
  assert.equal(fission.settlementDecision('takeover', 'completed'), 'complete-owner')
})
