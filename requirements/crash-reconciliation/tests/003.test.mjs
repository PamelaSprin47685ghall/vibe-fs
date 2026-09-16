import assert from 'node:assert/strict'
import test from 'node:test'
import * as reconcile from '../../../dist/Execution/Reconciliation/ReconcileObservationContractSurface.js'

test('WHAT[CRASH-003] unknown_effect_without_quiescence_is_not_replayed', () => {
  const decision = reconcile.decide({ effect: 'unknown', quiescence: false })
  assert.equal(decision.retry, false)
})

test('WHAT[CRASH-003] reconcile_decision_has_no_business_repair_vocabulary', () => {
  assert.doesNotMatch(reconcile.source(), /BusinessRepair|AutoFix|Heuristic/)
})
