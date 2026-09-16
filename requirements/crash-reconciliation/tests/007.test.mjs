import assert from 'node:assert/strict'
import test from 'node:test'
import * as reconcile from '../../../dist/Execution/Reconciliation/ReconcileObservationContractSurface.js'

test('WHAT[CRASH-007] turn_unknown_is_snapshot_observation_not_turn_outcome', () => {
  assert.equal(reconcile.isTurnOutcome('TurnUnknown'), false)
})

test('WHAT[CRASH-007] publish_boundary_carries_turn_outcome_not_snapshot_observation', () => {
  assert.equal(reconcile.isTurnOutcome('TurnCompleted'), true)
})
