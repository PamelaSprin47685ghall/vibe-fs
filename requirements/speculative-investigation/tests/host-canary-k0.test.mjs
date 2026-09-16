import assert from 'node:assert/strict'
import test from 'node:test'
import * as Strength from '../../../dist/Strength/Surface.js'
import { installDefaultResources } from '../../../dist/OpenCode/Host/ManagedAgentConfigSurface.js'

installDefaultResources()

const eligibleOpportunity = {
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
const decide = (opportunity, control = false, shadow = false, p = prediction, v = values, c = config) => Strength.policyDecide(opportunity, control, shadow, p, v, c)
const skipReason = (decision) => {
  assert.equal(decision.kind, 'Skip')
  return decision.reason
}

test('WHAT[SPEC-INV-001] STRENGTH_002_011_policy_k0_default_when_host_canary_or_cost_is_unproven', () => {
  const unhealthy = decide({ ...eligibleOpportunity, hostCanaryHealthy: false })
  assert.equal(skipReason(unhealthy), 'host-canary-unhealthy')
  assert.equal(unhealthy.budget, 'K0')
  const noPredictor = decide({ ...eligibleOpportunity, predictorAvailable: false })
  assert.equal(skipReason(noPredictor), 'predictor-unavailable')
  assert.equal(noPredictor.budget, 'K0')
  const noCost = decide({ ...eligibleOpportunity, costModelAvailable: false })
  assert.equal(skipReason(noCost), 'cost-model-unavailable')
  assert.equal(noCost.budget, 'K0')
  const shadow = decide(eligibleOpportunity, false, true)
  assert.equal(skipReason(shadow), 'shadow-k0')
  assert.equal(shadow.budget, 'K0')
})

test('WHAT[SPEC-INV-002] STRENGTH_002_013_review_finality_and_attached_internal_leaf_are_always_k0', () => {
  assert.equal(skipReason(decide({ ...eligibleOpportunity, canonicalRole: 'manager', selectedAgent: 'manager', effectiveAgent: 'manager' })), 'role-ineligible')
  assert.equal(decide({ ...eligibleOpportunity, isAttachedOrInternalLeaf: true }).budget, 'K0')
  const notRoot = decide({ ...eligibleOpportunity, isRootWork: false, isAttachedOrInternalLeaf: true })
  assert.equal(skipReason(notRoot), 'not-root-work')
  assert.equal(notRoot.budget, 'K0')
})

test('WHAT[SPEC-INV-002] STRENGTH_002_003_target_unbound_and_replica_request_kind_are_k0', () => {
  const unbound = decide({ ...eligibleOpportunity, targetProviderRunBound: false })
  assert.equal(skipReason(unbound), 'target-provider-run-unbound')
  assert.equal(unbound.budget, 'K0')
  const replicaKind = decide({ ...eligibleOpportunity, requestKind: 'strength-replica' })
  assert.equal(skipReason(replicaKind), 'not-work-main')
  assert.equal(replicaKind.budget, 'K0')
})

test('WHAT[SPEC-INV-010] STRENGTH_010_economic_holdout_is_not_skipped_and_ineligible_never_counts_as_holdout', () => {
  assert.equal(decide(eligibleOpportunity, true, false).kind, 'ControlHoldout')
  assert.equal(decide(eligibleOpportunity, true, false).budget, 'K0')
  const ineligibleHoldout = decide({ ...eligibleOpportunity, hostCanaryHealthy: false }, true, false)
  assert.equal(skipReason(ineligibleHoldout), 'host-canary-unhealthy')
  assert.notEqual(ineligibleHoldout.kind, 'ControlHoldout')
})

test('WHAT[SPEC-INV-010] STRENGTH_010_k2_is_gated_and_not_enabled_by_this_proof', () => {
  const belowFloor = decide(eligibleOpportunity, false, false, { ...prediction, evidenceCount: 19 })
  assert.equal(belowFloor.kind, 'Speculate')
  assert.equal(belowFloor.budget, 'K1')
  const equalMargin = decide(eligibleOpportunity, false, false, prediction, values, { K1Margin: 1, K2Margin: 1, K2MinimumEvidence: 20 })
  assert.equal(equalMargin.kind, 'Speculate')
  assert.equal(equalMargin.budget, 'K1')
})

test('WHAT[SPEC-INV-004] STRENGTH_014_policy_strength_replica_is_internal_leaf_attached_not_satellite_kind', () => {
  const facts = Strength.associationFacts('owner-work')
  assert.deepEqual(facts.satelliteCases, ['Companion'])
  assert.equal(facts.hasReplicaSatellite, false)
  assert.equal(facts.attachmentCases.includes('StrengthReplica'), true)
  assert.equal(facts.executionClass, 'InternalLeaf')
  assert.equal(facts.ownerSessionId, 'owner-work')
  assert.equal(facts.attachment, 'StrengthReplica')
  assert.equal(facts.strengthReplicaAttachment, true)
  assert.equal(facts.companionAttachment, false)
})


test('WHAT[SPEC-INV-004] STRENGTH_004_007_policy_same_role_prompt_has_no_replica_identity', () => {
  const coderId = Strength.systemPromptIdForRole('Coder')
  assert.equal(coderId, Strength.systemPromptIdForRole('Coder'))
  const prompt = Strength.systemPromptForRole('Coder')
  assert.ok(prompt.length > 0)
  assert.doesNotMatch(prompt, /strength|replica|prefetch/i)
})

test('WHAT[SPEC-INV-001] STRENGTH_001_014_policy_nested_replica_cannot_speculate', () => {
  const nested = decide({ ...eligibleOpportunity, isRootWork: false, isAttachedOrInternalLeaf: true, requestKind: 'strength-replica' })
  assert.equal(nested.budget, 'K0')
})
