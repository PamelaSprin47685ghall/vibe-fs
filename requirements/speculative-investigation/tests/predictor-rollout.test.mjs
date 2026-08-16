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
