import assert from 'node:assert/strict'
import test from 'node:test'
import * as reconcile from '../../../dist/Composition/Turn/ReconcileSurface.js'

const decisionName = (evidence, wake = reconcile.retryWake()) =>
  reconcile.decisionName(reconcile.decideStep(wake, evidence))

test('WHAT[crash-reconciliation-003] unknown_effect_without_quiescence_is_not_replayed', () => {
  // host-boundary-005: no read is authorized by a counter. Non-actionable
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

test.todo('WHAT[crash-reconciliation-003] actual reconciliation never replays an unresolved external effect before fresh physical evidence (GAP-149)')
