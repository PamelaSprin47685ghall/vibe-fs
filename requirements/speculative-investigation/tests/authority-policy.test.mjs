import assert from 'node:assert/strict'
import test from 'node:test'
import * as Strength from '../../../dist/Strength/Surface.js'

const exactReadonly = ['Glob', 'Grep', 'Read']
for (const role of ['Coder', 'Inspector', 'DevOps', 'Inquiry']) {
  test(`WHAT[SPEC-INV-004] STRENGTH_004_${role}_replica_has_exact_readonly_capabilities`, () => {
    assert.deepEqual(Strength.capabilities(role), exactReadonly)
  })
}
for (const role of ['Manager', 'Orchestrator', 'Browser', 'Reviewer', 'Distiller', 'Blogger']) {
  test(`WHAT[SPEC-INV-004] STRENGTH_004_${role}_replica_is_fail_closed`, () => {
    assert.deepEqual(Strength.capabilities(role), [])
  })
}

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
  canonicalRole: 'Coder',
  selectedTier: 'Deep',
  selectedAgent: 'deep-coder',
  effectiveAgent: 'deep-coder',
  isFallbackRetry: false,
  hasPrefixProbe: false,
  isReviewerOrFinality: false,
  isAttachedOrInternalLeaf: false,
  ownerCancelled: false,
  targetProviderRunBound: true,
  eventStoreHealthy: true,
  hostCanaryHealthy: true,
  fastPeerAvailable: true,
  costModelAvailable: true,
}
const prediction = { P1: 0.9, P2: 0.8, evidenceCount: 100 }
const values = { V0: 0, V1: 5, V2: 8 }
const config = { K1Margin: 1, K2Margin: 2, K2MinimumEvidence: 20 }

test('WHAT[SPEC-INV-002] STRENGTH_002_010_policy_rejects_unknown_role_tier_and_request_kind', () => {
  for (const field of ['canonicalRole', 'selectedTier', 'requestKind']) {
    const result = Strength.policyDecide({ ...base, [field]: 'unknown' }, false, false, prediction, values, config)
    assert.equal(result.ok, false)
    assert.match(result.error, /unknown (role|tier|request kind)/)
  }
})

test('WHAT[SPEC-INV-002] STRENGTH_002_010_policy_is_fail_closed_and_only_treats_proven_deep_opportunities', () => {
  assert.equal(Strength.policyDecide(base, false, false, prediction, values, config).kind, 'Speculate')
  assert.equal(Strength.policyDecide({ ...base, effectiveAgent: 'fast-coder' }, false, false, prediction, values, config).kind, 'Skip')
  assert.equal(Strength.policyDecide({ ...base, costModelAvailable: false }, false, false, prediction, values, config).kind, 'Skip')
  assert.equal(Strength.policyDecide(base, true, false, prediction, values, config).kind, 'ControlHoldout')
  assert.equal(Strength.policyDecide(base, false, true, prediction, values, config).kind, 'Skip')
})
