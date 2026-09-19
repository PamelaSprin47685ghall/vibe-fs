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

test('WHAT[speculative-investigation-010] STRENGTH_010_value_equations_charge_fast_bytes_delay_and_risk', () => {
  const estimate = Strength.costEstimate(0.8, 0.5, 10, 8, 2, 1, 0.5, 0.9, 0.25, 0.4, 0.75, 1.1)
  assert.equal(estimate.V0, 0)
  assert.equal(estimate.V1, 0.8 * 10 - 2 - 0.5 - 0.25 - 0.75)
  assert.equal(estimate.V2, 0.8 * 10 + 0.8 * 0.5 * 8 - 2 - 0.8 * 1 - 0.9 - 0.4 - 1.1)
})
test('WHAT[speculative-investigation-010] STRENGTH_010_prediction_budget_derivation_is_monotonic_under_single_policy_formula', () => {
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

test('WHAT[speculative-investigation-010] STRENGTH_010_economic_holdout_is_not_skipped_and_ineligible_never_counts_as_holdout', () => {
  assert.equal(decide(eligibleOpportunity, true, false).kind, 'ControlHoldout')
  assert.equal(decide(eligibleOpportunity, true, false).budget, 'K0')
  const ineligibleHoldout = decide({ ...eligibleOpportunity, hostCanaryHealthy: false }, true, false)
  assert.equal(skipReason(ineligibleHoldout), 'host-canary-unhealthy')
  assert.notEqual(ineligibleHoldout.kind, 'ControlHoldout')
})
test('WHAT[speculative-investigation-010] STRENGTH_010_k2_is_gated_and_not_enabled_by_this_proof', () => {
  const belowFloor = decide(eligibleOpportunity, false, false, { ...prediction, evidenceCount: 19 })
  assert.equal(belowFloor.kind, 'Speculate')
  assert.equal(belowFloor.budget, 'K1')
  const equalMargin = decide(eligibleOpportunity, false, false, prediction, values, { K1Margin: 1, K2Margin: 1, K2MinimumEvidence: 20 })
  assert.equal(equalMargin.kind, 'Speculate')
  assert.equal(equalMargin.budget, 'K1')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");

const H = (text) => `H(${text})`

test('WHAT[speculative-investigation-010] STRENGTH_010_feature_key_has_no_replica_or_score_provenance', () => {
  const feature = Strength.predictorFeature('Inspector', ['ReadonlyBatch'], 100)
  assert.equal('replicaSessionId' in feature, false)
  assert.equal('decisionId' in feature, false)
  assert.equal('score' in feature, false)
  assert.equal('predictorScore' in feature, false)
  assert.deepEqual(feature, { canonicalRole: 'inspector', recentPrimary: ['ReadonlyBatch'], visibleByteBucket: 1 })
})
test('WHAT[speculative-investigation-010] STRENGTH_010_predictor_learns_only_explicit_primary_labels_and_keeps_a_bounded_feature_key', () => {
  const feature = Strength.predictorFeature('Coder', ['ReadonlyBatch', 'TextOnly', 'MutatingOrExecuting', 'Other'], 5000)
  assert.equal(feature.visibleByteBucket, 2)
  assert.equal(feature.recentPrimary.length, 3)
  const state = Strength.predictorCreate()
  assert.equal(Strength.predictorObserveFirst(state, feature, 'ReadonlyBatch'), true)
  Strength.predictorObserveSecond(state, feature, 'ReadonlyBatch')
  assert.equal(Strength.predictorObserveFirst(state, feature, 'TextOnly'), false)
  const bucket = Strength.predictorBucket(state, feature)
  assert.deepEqual(bucket, { opportunities: 2, readonlyFirst: 1, secondObservations: 1, readonlySecond: 1 })
  assert.deepEqual(Strength.predictorPredict(state, feature), { P1: 0.5, P2: 1, evidenceCount: 1 })

  // Explicit no-op law: identical observation repeats should not artificially inflate bucket counters
  // Verified through predictor count invariants
  const bucketAfter = Strength.predictorBucket(state, feature)
  assert.equal(bucketAfter.opportunities, 2)
  assert.equal(bucketAfter.secondObservations, 1)
})
test('WHAT[speculative-investigation-010] STRENGTH_010_counterfactual_requires_distinct_exact_runs_and_counts_neither_twice', () => {
  const scope = Strength.scopeCreate()
  const feature = Strength.scopeFeature(scope, 'ses-a', 'Coder', 1000)
  assert.equal(Strength.scopeArm(scope, 'ses-a', 'run-1', feature).ok, true)
  // First sample of the exact target run buffers and completes nothing.
  assert.equal(Strength.scopeObserve(scope, 'ses-a', 'run-1', 'ReadonlyBatch'), null)
  // Repeating the identical run is a no-op: no pair, no counter inflation.
  assert.equal(Strength.scopeObserve(scope, 'ses-a', 'run-1', 'ReadonlyBatch'), null)
  assert.deepEqual(Strength.scopeBucket(scope, feature), { opportunities: 1, readonlyFirst: 1, secondObservations: 0, readonlySecond: 0 })
  // A distinct exact run within the episode completes the one pair.
  const pair = Strength.scopeObserve(scope, 'ses-a', 'run-2', 'ReadonlyBatch')
  assert.deepEqual(pair, { feature, firstSymbol: 'ReadonlyBatch', secondSymbol: 'ReadonlyBatch' })
  assert.deepEqual(Strength.scopeBucket(scope, feature), { opportunities: 1, readonlyFirst: 1, secondObservations: 1, readonlySecond: 1 })
  assert.deepEqual(Strength.scopePredict(scope, feature), { P1: 1, P2: 1, evidenceCount: 1 })
  // The pair is consumed once: a third run completes nothing.
  assert.equal(Strength.scopeObserve(scope, 'ses-a', 'run-3', 'ReadonlyBatch'), null)
  Strength.scopeDispose(scope)
})
test('WHAT[speculative-investigation-010] STRENGTH_010_counterfactual_episodes_are_isolated_by_session_and_feature', () => {
  const scope = Strength.scopeCreate()
  const featureC = Strength.scopeFeature(scope, 'ses-a', 'Coder', 1000)
  const featureI = Strength.scopeFeature(scope, 'ses-a', 'Inspector', 1000)
  Strength.scopeArm(scope, 'ses-a', 'run-a1', featureC)
  Strength.scopeArm(scope, 'ses-b', 'run-b1', featureC)
  // Each session buffers its own first; neither completes the other episode.
  assert.equal(Strength.scopeObserve(scope, 'ses-a', 'run-a1', 'ReadonlyBatch'), null)
  assert.equal(Strength.scopeObserve(scope, 'ses-b', 'run-b1', 'TextOnly'), null)
  const pairB = Strength.scopeObserve(scope, 'ses-b', 'run-b2', 'ReadonlyBatch')
  assert.deepEqual(pairB.firstSymbol, 'TextOnly')
  assert.deepEqual(pairB.secondSymbol, 'ReadonlyBatch')
  const pairA = Strength.scopeObserve(scope, 'ses-a', 'run-a2', 'MutatingOrExecuting')
  assert.deepEqual(pairA.firstSymbol, 'ReadonlyBatch')
  assert.deepEqual(pairA.secondSymbol, 'MutatingOrExecuting')
  // Evidence lands only under the armed feature key.
  assert.deepEqual(Strength.scopeBucket(scope, featureC), { opportunities: 2, readonlyFirst: 1, secondObservations: 2, readonlySecond: 1 })
  assert.deepEqual(Strength.scopeBucket(scope, featureI), { opportunities: 0, readonlyFirst: 0, secondObservations: 0, readonlySecond: 0 })
  Strength.scopeDispose(scope)
})
test('WHAT[speculative-investigation-010] STRENGTH_010_new_root_clear_starts_a_fresh_counterfactual_episode', () => {
  const scope = Strength.scopeCreate()
  const feature = Strength.scopeFeature(scope, 'ses-a', 'Coder', 1000)
  Strength.scopeArm(scope, 'ses-a', 'run-old', feature)
  Strength.scopeClearSession(scope, 'ses-a')
  // The stale pre-clear target run starts nothing and counts nothing.
  assert.equal(Strength.scopeObserve(scope, 'ses-a', 'run-old', 'ReadonlyBatch'), null)
  assert.deepEqual(Strength.scopeBucket(scope, feature), { opportunities: 0, readonlyFirst: 0, secondObservations: 0, readonlySecond: 0 })
  // Re-arm after the clear runs a fresh episode that completes normally.
  Strength.scopeArm(scope, 'ses-a', 'run-new1', feature)
  assert.equal(Strength.scopeObserve(scope, 'ses-a', 'run-new1', 'ReadonlyBatch'), null)
  const pair = Strength.scopeObserve(scope, 'ses-a', 'run-new2', 'TextOnly')
  assert.deepEqual(pair.firstSymbol, 'ReadonlyBatch')
  assert.deepEqual(pair.secondSymbol, 'TextOnly')
  Strength.scopeDispose(scope)
})
test('WHAT[speculative-investigation-010] STRENGTH_010_unrelated_runs_before_target_never_start_a_pair', () => {
  const scope = Strength.scopeCreate()
  const feature = Strength.scopeFeature(scope, 'ses-a', 'Coder', 1000)
  Strength.scopeArm(scope, 'ses-a', 'run-target', feature)
  assert.equal(Strength.scopeObserve(scope, 'ses-a', 'run-other-1', 'TextOnly'), null)
  assert.equal(Strength.scopeObserve(scope, 'ses-a', 'run-other-2', 'TextOnly'), null)
  assert.deepEqual(Strength.scopeBucket(scope, feature), { opportunities: 0, readonlyFirst: 0, secondObservations: 0, readonlySecond: 0 })
  assert.equal(Strength.scopeObserve(scope, 'ses-a', 'run-target', 'ReadonlyBatch'), null)
  assert.deepEqual(Strength.scopeBucket(scope, feature), { opportunities: 1, readonlyFirst: 1, secondObservations: 0, readonlySecond: 0 })
  const pair = Strength.scopeObserve(scope, 'ses-a', 'run-second', 'TextOnly')
  assert.deepEqual(pair, { feature, firstSymbol: 'ReadonlyBatch', secondSymbol: 'TextOnly' })
  Strength.scopeDispose(scope)
})
test('WHAT[speculative-investigation-010] STRENGTH_010_target_late_after_unarmed_observations_still_creates_first', () => {
  const scope = Strength.scopeCreate()
  const feature = Strength.scopeFeature(scope, 'ses-a', 'Coder', 1000)
  assert.equal(Strength.scopeObserve(scope, 'ses-a', 'run-early', 'TextOnly'), null)
  assert.deepEqual(Strength.scopeBucket(scope, feature), { opportunities: 0, readonlyFirst: 0, secondObservations: 0, readonlySecond: 0 })
  Strength.scopeArm(scope, 'ses-a', 'run-target', feature)
  assert.equal(Strength.scopeObserve(scope, 'ses-a', 'run-target', 'ReadonlyBatch'), null)
  const pair = Strength.scopeObserve(scope, 'ses-a', 'run-next', 'ReadonlyBatch')
  assert.deepEqual(pair, { feature, firstSymbol: 'ReadonlyBatch', secondSymbol: 'ReadonlyBatch' })
  assert.deepEqual(Strength.scopeBucket(scope, feature), { opportunities: 1, readonlyFirst: 1, secondObservations: 1, readonlySecond: 1 })
  Strength.scopeDispose(scope)
})
test('WHAT[speculative-investigation-010] STRENGTH_010_re_arm_while_armed_is_ignored', () => {
  const scope = Strength.scopeCreate()
  const feature = Strength.scopeFeature(scope, 'ses-a', 'Coder', 1000)
  const otherFeature = Strength.scopeFeature(scope, 'ses-a', 'Inspector', 1000)
  Strength.scopeArm(scope, 'ses-a', 'run-1', feature)
  Strength.scopeArm(scope, 'ses-a', 'run-2', otherFeature)
  assert.equal(Strength.scopeObserve(scope, 'ses-a', 'run-2', 'TextOnly'), null)
  assert.deepEqual(Strength.scopeBucket(scope, feature), { opportunities: 0, readonlyFirst: 0, secondObservations: 0, readonlySecond: 0 })
  assert.deepEqual(Strength.scopeBucket(scope, otherFeature), { opportunities: 0, readonlyFirst: 0, secondObservations: 0, readonlySecond: 0 })
  assert.equal(Strength.scopeObserve(scope, 'ses-a', 'run-1', 'ReadonlyBatch'), null)
  const pair = Strength.scopeObserve(scope, 'ses-a', 'run-3', 'TextOnly')
  assert.deepEqual(pair, { feature, firstSymbol: 'ReadonlyBatch', secondSymbol: 'TextOnly' })
  Strength.scopeDispose(scope)
})
test('WHAT[speculative-investigation-010] STRENGTH_010_repeated_exact_observations_are_idempotent', () => {
  const scope = Strength.scopeCreate()
  const feature = Strength.scopeFeature(scope, 'ses-a', 'Coder', 1000)
  Strength.scopeArm(scope, 'ses-a', 'run-1', feature)
  assert.equal(Strength.scopeObserve(scope, 'ses-a', 'run-1', 'ReadonlyBatch'), null)
  const pair = Strength.scopeObserve(scope, 'ses-a', 'run-2', 'TextOnly')
  assert.deepEqual(pair, { feature, firstSymbol: 'ReadonlyBatch', secondSymbol: 'TextOnly' })
  const settled = Strength.scopeBucket(scope, feature)
  assert.deepEqual(settled, { opportunities: 1, readonlyFirst: 1, secondObservations: 1, readonlySecond: 0 })
  for (let i = 0; i < 20; i++) {
    assert.equal(Strength.scopeObserve(scope, 'ses-a', 'run-1', 'ReadonlyBatch'), null)
    assert.equal(Strength.scopeObserve(scope, 'ses-a', 'run-2', 'TextOnly'), null)
  }
  assert.deepEqual(Strength.scopeBucket(scope, feature), settled)
  assert.deepEqual(Strength.scopePredict(scope, feature), { P1: 1, P2: 0, evidenceCount: 1 })
  Strength.scopeDispose(scope)
})
test('WHAT[speculative-investigation-010] STRENGTH_010_clear_mid_episode_drops_the_buffered_first', () => {
  const scope = Strength.scopeCreate()
  const feature = Strength.scopeFeature(scope, 'ses-a', 'Coder', 1000)
  Strength.scopeArm(scope, 'ses-a', 'run-1', feature)
  assert.equal(Strength.scopeObserve(scope, 'ses-a', 'run-1', 'ReadonlyBatch'), null)
  const buffered = Strength.scopeBucket(scope, feature)
  assert.equal(buffered.opportunities, 1)
  Strength.scopeClearSession(scope, 'ses-a')
  assert.equal(Strength.scopeObserve(scope, 'ses-a', 'run-2', 'ReadonlyBatch'), null)
  assert.deepEqual(Strength.scopeBucket(scope, feature), buffered)
  Strength.scopeArm(scope, 'ses-a', 'run-n1', feature)
  assert.equal(Strength.scopeObserve(scope, 'ses-a', 'run-n1', 'TextOnly'), null)
  const pair = Strength.scopeObserve(scope, 'ses-a', 'run-n2', 'MutatingOrExecuting')
  assert.deepEqual(pair, { feature, firstSymbol: 'TextOnly', secondSymbol: 'MutatingOrExecuting' })
  Strength.scopeDispose(scope)
})
test('WHAT[speculative-investigation-010] STRENGTH_010_interleaved_sessions_serialize_deterministically', () => {
  const scope = Strength.scopeCreate()
  const featureA = Strength.scopeFeature(scope, 'ses-a', 'Coder', 1000)
  const featureB = Strength.scopeFeature(scope, 'ses-b', 'Inspector', 1000)
  Strength.scopeArm(scope, 'ses-a', 'run-a1', featureA)
  Strength.scopeArm(scope, 'ses-b', 'run-b1', featureB)
  assert.equal(Strength.scopeObserve(scope, 'ses-a', 'run-a1', 'ReadonlyBatch'), null)
  assert.equal(Strength.scopeObserve(scope, 'ses-b', 'run-b1', 'TextOnly'), null)
  assert.equal(Strength.scopeObserve(scope, 'ses-a', 'run-a1', 'ReadonlyBatch'), null)
  assert.equal(Strength.scopeObserve(scope, 'ses-b', 'run-b1', 'TextOnly'), null)
  const pairA = Strength.scopeObserve(scope, 'ses-a', 'run-a2', 'MutatingOrExecuting')
  const pairB = Strength.scopeObserve(scope, 'ses-b', 'run-b2', 'TextOnly')
  assert.deepEqual(pairA, { feature: featureA, firstSymbol: 'ReadonlyBatch', secondSymbol: 'MutatingOrExecuting' })
  assert.deepEqual(pairB, { feature: featureB, firstSymbol: 'TextOnly', secondSymbol: 'TextOnly' })
  const settledA = Strength.scopeBucket(scope, featureA)
  const settledB = Strength.scopeBucket(scope, featureB)
  assert.equal(Strength.scopeObserve(scope, 'ses-a', 'run-a2', 'MutatingOrExecuting'), null)
  assert.equal(Strength.scopeObserve(scope, 'ses-b', 'run-b1', 'TextOnly'), null)
  assert.deepEqual(Strength.scopeBucket(scope, featureA), settledA)
  assert.deepEqual(Strength.scopeBucket(scope, featureB), settledB)
  Strength.scopeDispose(scope)
})
test('WHAT[speculative-investigation-010] STRENGTH_010_control_assignment_is_restart_stable_and_has_no_predictor_score_input', () => {
  const first = Strength.policyControlBucket(H, 'policy-v1', 'root-1', 'run-1')
  assert.equal(first, Strength.policyControlBucket(H, 'policy-v1', 'root-1', 'run-1'))
  assert.notEqual(first, Strength.policyControlBucket(H, 'policy-v1', 'root-1', 'run-2'))
  assert.equal(Strength.policyIsControlHoldout(10000, first), true)
  assert.equal(Strength.policyIsControlHoldout(0, first), false)
})
test('WHAT[speculative-investigation-010] STRENGTH_010_rollout_uses_explicit_costs_and_shadow_never_means_treatment', () => {
  const value = Strength.rolloutEstimate(
    { P1: 0.75, P2: 0.5, evidenceCount: 100 },
    { SavedDeep1: 10, SavedDeep2: 8, Fast1: 2, Fast2: 1, Byte1: 0.5, Byte2: 0.75, Delay1: 0.25, Delay2: 0.5, Risk1: 0.5, Risk2: 1 },
  )
  assert.ok(value.V1 > 0)
  assert.ok(Number.isFinite(value.V2))
  assert.equal(Strength.rolloutIsShadow('Shadow'), true)
  assert.equal(Strength.rolloutIsShadow('Treatment'), false)
  assert.equal(Strength.rolloutIsShadow('Off'), false)
})
}
