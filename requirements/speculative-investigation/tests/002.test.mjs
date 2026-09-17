import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");

const exactReadonly = ['Glob', 'Grep', 'Read']
const base = {
  isRootWork: true,
  requestKind: 'work-main',
  canonicalRole: 'coder',
  selectedAgent: 'coder',
  hasPrefixProbe: false,
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

test('WHAT[SPEC-INV-002] STRENGTH_002_010_policy_rejects_unknown_role_and_request_kind', () => {
  for (const field of ['canonicalRole', 'requestKind']) {
    const result = Strength.policyDecide({ ...base, [field]: 'unknown' }, false, false, prediction, values, config)
    assert.equal(result.ok, false)
    assert.match(result.error, /unknown (role|request kind)/)
  }
})
test('WHAT[SPEC-INV-002] STRENGTH_002_010_policy_is_fail_closed_and_only_treats_proven_fixed_identity_opportunities', () => {
  assert.equal(Strength.policyDecide(base, false, false, prediction, values, config).kind, 'Speculate')
  assert.equal(Strength.policyDecide({ ...base, isRootWork: false }, false, false, prediction, values, config).kind, 'Skip')
  assert.equal(Strength.policyDecide({ ...base, predictorAvailable: false }, false, false, prediction, values, config).kind, 'Skip')
  assert.equal(Strength.policyDecide({ ...base, costModelAvailable: false }, false, false, prediction, values, config).kind, 'Skip')
  assert.equal(Strength.policyDecide(base, true, false, prediction, values, config).kind, 'ControlHoldout')
  assert.equal(Strength.policyDecide(base, false, true, prediction, values, config).kind, 'Skip')
})
test('WHAT[SPEC-INV-002] STRENGTH_002_speculation_opportunity_eligibility_is_pure_frozen_evidence_without_wall_clock_state', () => {
  const dec1 = Strength.policyDecide(base, false, false, prediction, values, config)
  const dec2 = Strength.policyDecide(base, false, false, prediction, values, config)
  assert.deepEqual(dec1, dec2)
  assert.equal(dec1.kind, 'Speculate')
  assert.equal(dec1.budget, 'K2')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");
const { installDefaultResources } = await import("../../../dist/OpenCode/Host/ManagedAgentConfigSurface.js");

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
}

{
const { default: assert } = await import("node:assert/strict");
const { readFileSync } = await import("node:fs");
const { resolve } = await import("node:path");
const { default: test } = await import("node:test");

const root = resolve(import.meta.dirname, '../../..')
const read = (path) => readFileSync(resolve(root, path), 'utf8')

test('WHAT[SPEC-INV-002] StrengthSpeculate owns tryApply entry point for transform speculation', () => {
  const speculate = read('src/Wanxiangshu/Strength/OpenCode/Speculate.fs')
  const pt = read('src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs')

  assert.match(speculate, /let\s+tryApply/)
  assert.match(speculate, /applyBoundOwner/)
  assert.match(speculate, /XTraceCapture\.stableCaptureEligibility/)
  assert.doesNotMatch(speculate, /XTraceCapture\.supportsStableInsertion/)
  assert.doesNotMatch(speculate, /XTraceCapture\.capture\w+/,
    'speculative presentation must never capture unconfirmed material into canonical XTrace')
  assert.match(pt, /StrengthSpeculate\.tryApply/)
})
test('WHAT[SPEC-INV-002] STRENGTH_002_replica_transform_mode_is_explicit_semantic_alternative_in_composition_root', () => {
  const pt = read('src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs')

  assert.match(pt, /type\s+private\s+TransformMode\s*=\s*\|\s*ExplicitResumeDisclosure\s*\|\s*StrengthReplica\s+of\s+StrengthReplicaRuntime\s*\|\s*Ordinary/)
  assert.match(pt, /let\s+private\s+determineTransformMode/)
  assert.match(pt, /match\s+determineTransformMode\s+branches/)
  assert.match(pt, /\|\s*StrengthReplica\s+runtime\s*->/)
  assert.match(pt, /branches\.ReplicaXWire/)
  assert.match(pt, /runtime\.HandleTransform/)
  assert.match(pt, /failIfReplicaDecisionLost/)
  assert.match(pt, /branches\.ReplicaSanitize/)
})
}
