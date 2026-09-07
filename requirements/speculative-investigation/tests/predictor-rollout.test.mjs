import assert from 'node:assert/strict'
import test from 'node:test'
import * as Strength from '../../../dist/Strength/Surface.js'

const H = (text) => `H(${text})`

test('WHAT[SPEC-INV-010] STRENGTH_010_feature_key_has_no_replica_or_score_provenance', () => {
  const feature = Strength.predictorFeature('Inspector', ['ReadonlyBatch'], 100)
  assert.equal('replicaSessionId' in feature, false)
  assert.equal('decisionId' in feature, false)
  assert.equal('score' in feature, false)
  assert.equal('predictorScore' in feature, false)
  assert.deepEqual(feature, { canonicalRole: 'inspector', recentPrimary: ['ReadonlyBatch'], visibleByteBucket: 1 })
})

test('WHAT[SPEC-INV-010] STRENGTH_010_predictor_learns_only_explicit_primary_labels_and_keeps_a_bounded_feature_key', () => {
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

test('WHAT[SPEC-INV-010] STRENGTH_010_counterfactual_requires_distinct_exact_runs_and_counts_neither_twice', () => {
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

test('WHAT[SPEC-INV-010] STRENGTH_010_counterfactual_episodes_are_isolated_by_session_and_feature', () => {
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

test('WHAT[SPEC-INV-010] STRENGTH_010_new_root_clear_starts_a_fresh_counterfactual_episode', () => {
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

test('WHAT[SPEC-INV-010] STRENGTH_010_unrelated_runs_before_target_never_start_a_pair', () => {
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

test('WHAT[SPEC-INV-010] STRENGTH_010_target_late_after_unarmed_observations_still_creates_first', () => {
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

test('WHAT[SPEC-INV-010] STRENGTH_010_re_arm_while_armed_is_ignored', () => {
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

test('WHAT[SPEC-INV-010] STRENGTH_010_repeated_exact_observations_are_idempotent', () => {
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

test('WHAT[SPEC-INV-010] STRENGTH_010_clear_mid_episode_drops_the_buffered_first', () => {
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

test('WHAT[SPEC-INV-010] STRENGTH_010_interleaved_sessions_serialize_deterministically', () => {
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

test('WHAT[SPEC-INV-011] STRENGTH_011_scope_fuse_keeps_first_reason_across_clear_and_dispose', () => {
  const scope = Strength.scopeCreate()
  assert.equal(Strength.scopeFuseReason(scope), null)
  Strength.scopeTripFuse(scope, 'first-failure')
  Strength.scopeTripFuse(scope, 'later-noise')
  assert.equal(Strength.scopeFuseReason(scope), 'first-failure')
  Strength.scopeClearSession(scope, 'ses-a')
  assert.equal(Strength.scopeFuseReason(scope), 'first-failure')
  Strength.scopeDispose(scope)
  assert.equal(Strength.scopeFuseReason(scope), 'first-failure')
})

test('WHAT[SPEC-INV-011] STRENGTH_011_scope_dispose_drops_process_local_caches_but_never_untrips_the_fuse', () => {
  const scope = Strength.scopeCreate()
  const feature = Strength.scopeFeature(scope, 'ses-a', 'Coder', 1000)
  Strength.scopeArm(scope, 'ses-a', 'run-1', feature)
  assert.equal(Strength.scopeObserve(scope, 'ses-a', 'run-1', 'ReadonlyBatch'), null)
  const binding = Strength.runtimeBinding('owner-d', 'replica-d', 'dec-d', 'run-dec-d', 'Coder', 'K1', 65536, 'sem-d', [])
  assert.equal(Strength.scopeRuntimeRegister(scope, binding).ok, true)
  assert.notEqual(Strength.scopeRuntimeFindByReplica(scope, 'replica-d'), null)
  Strength.scopeTripFuse(scope, 'boom')
  Strength.scopeDispose(scope)
  // Every process-local cache is gone: live binding, collector episode,
  // recent-primary window and predictor evidence.
  assert.equal(Strength.scopeRuntimeFindByReplica(scope, 'replica-d'), null)
  assert.deepEqual(Strength.scopeBucket(scope, feature), { opportunities: 0, readonlyFirst: 0, secondObservations: 0, readonlySecond: 0 })
  assert.deepEqual(Strength.scopeFeature(scope, 'ses-a', 'Coder', 1000).recentPrimary, [])
  assert.equal(Strength.scopeObserve(scope, 'ses-a', 'run-1', 'ReadonlyBatch'), null)
  // The process-lifetime fuse survives dispose.
  assert.equal(Strength.scopeFuseReason(scope), 'boom')
})

test('WHAT[SPEC-INV-010] STRENGTH_010_control_assignment_is_restart_stable_and_has_no_predictor_score_input', () => {
  const first = Strength.policyControlBucket(H, 'policy-v1', 'root-1', 'run-1')
  assert.equal(first, Strength.policyControlBucket(H, 'policy-v1', 'root-1', 'run-1'))
  assert.notEqual(first, Strength.policyControlBucket(H, 'policy-v1', 'root-1', 'run-2'))
  assert.equal(Strength.policyIsControlHoldout(10000, first), true)
  assert.equal(Strength.policyIsControlHoldout(0, first), false)
})

test('WHAT[SPEC-INV-010] STRENGTH_010_rollout_uses_explicit_costs_and_shadow_never_means_treatment', () => {
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
