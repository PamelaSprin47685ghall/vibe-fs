import assert from 'node:assert/strict'
import test from 'node:test'
import * as Strength from '../../../dist/Strength/Surface.js'

const exactReadonly = ['Glob', 'Grep', 'Read']
test('WHAT[SPEC-INV-004] STRENGTH_004_each_supported_replica_has_exact_readonly_capabilities', () => {
  for (const role of ['Coder', 'Inspector', 'DevOps', 'Inquiry']) {
    assert.deepEqual(Strength.capabilities(role), exactReadonly)
  }
})
test('WHAT[SPEC-INV-004] STRENGTH_004_each_unsupported_replica_is_fail_closed', () => {
  for (const role of ['Manager', 'Orchestrator', 'Browser', 'Reviewer', 'Distiller', 'Blogger']) {
    assert.deepEqual(Strength.capabilities(role), [])
  }
})

test('WHAT[SPEC-INV-004] STRENGTH_004_019_replica_is_never_owner_fallback_or_prefix_probe_evidence', () => {
  assert.equal(Strength.clearsFailureCountOnSuccess('strength-replica'), false)
  assert.equal(Strength.mayCarryProbe('strength-replica'), false)
  assert.equal(Strength.clearsFailureCountOnSuccess('work-main'), true)
  assert.equal(Strength.mayCarryProbe('work-main'), true)
})

test('WHAT[SPEC-INV-010] STRENGTH_010_value_equations_charge_fast_bytes_delay_and_risk', () => {
  const estimate = Strength.costEstimate(0.8, 0.5, 10, 8, 2, 1, 0.5, 0.9, 0.25, 0.4, 0.75, 1.1)
  assert.equal(estimate.V0, 0)
  assert.equal(estimate.V1, 0.8 * 10 - 2 - 0.5 - 0.25 - 0.75)
  assert.equal(estimate.V2, 0.8 * 10 + 0.8 * 0.5 * 8 - 2 - 0.8 * 1 - 0.9 - 0.4 - 1.1)
})

const base = {
  isRootWork: true,
  requestKind: 'work-main',
  canonicalRole: 'coder',
  selectedAgent: 'coder',
  effectiveAgent: 'coder',
  isFallbackRetry: false,
  hasPrefixProbe: false,
  isReviewerOrFinality: false,
  isAttachedOrInternalLeaf: false,
  ownerCancelled: false,
  targetProviderRunBound: true,
  eventStoreHealthy: true,
  hostCanaryHealthy: true,
  predictorAvailable: true,
  costModelAvailable: true,
}
const prediction = { P1: 0.9, P2: 0.8, evidenceCount: 100 }
const values = { V0: 0, V1: 5, V2: 8 }
const config = { K1Margin: 1, K2Margin: 2, K2MinimumEvidence: 20 }

test('WHAT[SPEC-INV-002] STRENGTH_002_010_policy_rejects_unknown_role_tier_and_request_kind', () => {
  for (const field of ['canonicalRole', 'requestKind']) {
    const result = Strength.policyDecide({ ...base, [field]: 'unknown' }, false, false, prediction, values, config)
    assert.equal(result.ok, false)
    assert.match(result.error, /unknown (role|request kind)/)
  }
})

test('WHAT[SPEC-INV-002] STRENGTH_002_010_policy_is_fail_closed_and_only_treats_proven_deep_opportunities', () => {
  assert.equal(Strength.policyDecide(base, false, false, prediction, values, config).kind, 'Speculate')
  assert.equal(Strength.policyDecide({ ...base, effectiveAgent: 'inspector' }, false, false, prediction, values, config).kind, 'Skip')
  assert.equal(Strength.policyDecide({ ...base, predictorAvailable: false }, false, false, prediction, values, config).kind, 'Skip')
  assert.equal(Strength.policyDecide({ ...base, costModelAvailable: false }, false, false, prediction, values, config).kind, 'Skip')
  assert.equal(Strength.policyDecide(base, true, false, prediction, values, config).kind, 'ControlHoldout')
  assert.equal(Strength.policyDecide(base, false, true, prediction, values, config).kind, 'Skip')
})

test('WHAT[SPEC-INV-010] STRENGTH_010_prediction_budget_derivation_is_monotonic_under_single_policy_formula', () => {
  // Single formula owner StrengthPolicy.decideFromFacts
  // 1. Non-positive value estimate -> budget K0 (Skip)
  const lowV1 = { V0: 0, V1: 0.5, V2: 1.0 }
  const decLow = Strength.policyDecide(base, false, false, prediction, lowV1, config)
  assert.equal(decLow.kind, 'Skip')
  assert.equal(decLow.budget, 'K0')

  // 2. V1 > K1Margin, but V2 not high enough -> budget K1 (Speculate K1)
  const medV = { V0: 0, V1: 3.0, V2: 4.0 }
  const decMed = Strength.policyDecide(base, false, false, prediction, medV, config)
  assert.equal(decMed.kind, 'Speculate')
  assert.equal(decMed.budget, 'K1')

  // 3. V1 > K1Margin and V2 > V1 + K2Margin with enough evidence -> budget K2 (Speculate K2)
  const highV = { V0: 0, V1: 3.0, V2: 6.0 }
  const decHigh = Strength.policyDecide(base, false, false, prediction, highV, config)
  assert.equal(decHigh.kind, 'Speculate')
  assert.equal(decHigh.budget, 'K2')

  // 4. Monotonicity: K2 condition cannot activate if K1 is not worthwhile
  const invV = { V0: 0, V1: 0.5, V2: 10.0 }
  const decInv = Strength.policyDecide(base, false, false, prediction, invV, config)
  assert.equal(decInv.kind, 'Skip')
  assert.equal(decInv.budget, 'K0')
})

test('WHAT[SPEC-INV-002] STRENGTH_002_speculation_opportunity_eligibility_is_pure_frozen_evidence_without_wall_clock_state', () => {
  const dec1 = Strength.policyDecide(base, false, false, prediction, values, config)
  const dec2 = Strength.policyDecide(base, false, false, prediction, values, config)
  assert.deepEqual(dec1, dec2)
  assert.equal(dec1.kind, 'Speculate')
  assert.equal(dec1.budget, 'K2')
})
