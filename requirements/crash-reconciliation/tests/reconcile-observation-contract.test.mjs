// CRASH-003 / CRASH-007 contract through the production Reconcile owner
// surface. Wake, evidence, decision, and publish maps cross as plain data;
// publish maps remain opaque handles.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as reconcile from '../../../dist/Composition/Turn/ReconcileSurface.js'

const decisionName = (evidence, wake = reconcile.retryWake()) =>
  reconcile.decisionName(reconcile.decideStep(wake, evidence))

test('WHAT[CRASH-003] unknown_effect_without_quiescence_is_not_replayed', () => {
  // HOST-BOUNDARY-005: no read is authorized by a counter. Non-actionable
  // evidence stops without replay. Unknown only publishes after a fresh
  // idle/quiescence observation.
  assert.equal(decisionName(reconcile.evidenceSnapshotError('transient')), 'StopPass')
  assert.equal(decisionName(reconcile.evidenceNoTurn()), 'StopPass')

  assert.equal(decisionName(reconcile.evidenceUnknown(), reconcile.retryWake()), 'StopPass')
  assert.equal(decisionName(reconcile.evidenceUnknown(), reconcile.failureWake()), 'StopPass')
  assert.equal(decisionName(reconcile.evidenceUnknown(), reconcile.abortWake()), 'StopPass')

  assert.equal(
    decisionName(reconcile.evidenceUnknown(), reconcile.idleWake('ses-a', 1)),
    'Publish',
  )
})

test('WHAT[CRASH-003] reconcile_decision_has_no_business_repair_vocabulary', () => {
  // The owner exposes only observation decisions. Exercise every decision shape
  // through the JS-native surface rather than reflecting a Fable union.
  const names = new Set([
    decisionName(reconcile.evidenceUnknown(), reconcile.retryWake()),
    decisionName(reconcile.evidenceUnknown(), reconcile.idleWake('ses-a', 1)),
    decisionName(reconcile.evidenceNoTurn(), reconcile.retryWake()),
  ])
  assert.deepEqual([...names].sort(), ['Publish', 'StopPass'])
  assert.equal([...names].some((name) => /Repair|Resend|Rollback|Abort|Replay|Reread/i.test(name)), false)
})

test('WHAT[CRASH-007] turn_unknown_is_snapshot_observation_not_turn_outcome', () => {
  const publishable = ['TurnInProgress', 'TurnNeedsContinuation', 'TurnCompleted', 'TurnAborted', 'TurnFailed']
  for (const name of publishable) {
    assert.equal(reconcile.tryOutcome(name).accepted, true, `${name} must be a publishable outcome`)
    assert.equal(reconcile.isPublishableOutcome(name), true, `${name} must remain publishable`)
  }

  assert.equal(reconcile.tryOutcome('TurnUnknown').accepted, false)
  assert.equal(reconcile.isSnapshotObservation('TurnUnknown'), true)
  assert.equal(reconcile.isPublishableOutcome('TurnUnknown'), false)
})

test('WHAT[CRASH-007] publish_boundary_carries_turn_outcome_not_snapshot_observation', () => {
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
