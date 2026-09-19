import assert from 'node:assert/strict'
import test from 'node:test'
import * as reconcile from '../../../dist/Composition/Turn/ReconcileSurface.js'

const decisionName = (evidence, wake = reconcile.retryWake()) =>
  reconcile.decisionName(reconcile.decideStep(wake, evidence))

test('WHAT[crash-reconciliation-007] turn_unknown_is_snapshot_observation_not_turn_outcome', () => {
  const publishable = ['TurnInProgress', 'TurnNeedsContinuation', 'TurnCompleted', 'TurnAborted', 'TurnFailed']
  for (const name of publishable) {
    assert.equal(reconcile.tryOutcome(name).accepted, true, `${name} must be a publishable outcome`)
    assert.equal(reconcile.isPublishableOutcome(name), true, `${name} must remain publishable`)
  }

  assert.equal(reconcile.tryOutcome('TurnUnknown').accepted, false)
  assert.equal(reconcile.isSnapshotObservation('TurnUnknown'), true)
  assert.equal(reconcile.isPublishableOutcome('TurnUnknown'), false)
})

test('WHAT[crash-reconciliation-007] publish_boundary_carries_turn_outcome_not_snapshot_observation', () => {
  // This is the owner-defined plain input contract, not Fable reflection.
  assert.deepEqual(reconcile.acceptedTurnFields(), ['session', 'physical', 'providerRun', 'outcome'])

  const terminal = reconcile.turnFixture({
    session: 'ses-a',
    physical: 'user-1',
    providerRun: 'asst-1',
    outcome: 'TurnCompleted',
  })
  const first = reconcile.publishDecision(reconcile.empty(), terminal)
  assert.equal(first.shouldPublish, true)
  const second = reconcile.publishDecision(first.maps, terminal)
  assert.equal(second.shouldPublish, false, 'same completion must be sealed once (dedupe, no replay)')

  // A snapshot observation has no business outcome constructor and therefore
  // cannot cross the publish handoff.
  const unknown = reconcile.turnFixture({
    session: 'ses-a',
    physical: 'user-unknown',
    providerRun: 'asst-unknown',
    outcome: 'TurnUnknown',
  })
  assert.throws(() => reconcile.publishDecision(reconcile.empty(), unknown), /TurnUnknown|outcome/i)
})
