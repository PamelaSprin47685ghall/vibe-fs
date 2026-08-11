import assert from 'node:assert/strict'
import test from 'node:test'

import * as Predictor from '../../../dist/Domain/StrengthPredictor.js'
import * as Rollout from '../../../dist/Domain/StrengthRollout.js'
import * as Policy from '../../../dist/Domain/StrengthPolicy.js'
import * as HostDigest from '../../../dist/Host/HostDigest.js'
import { Role } from '../../../dist/Kernel/Roles.js'
import { ofArray as toList } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'

const S = Predictor.StrengthPrimarySymbol

test('STRENGTH_010_predictor_learns_only_explicit_primary_labels_and_keeps_a_bounded_feature_key', () => {
  const feature = Predictor.StrengthPredictor_feature(
    Role.Coder,
    toList([S.ReadonlyBatch, S.TextOnly, S.MutatingOrExecuting, S.Other]),
    5000,
  )

  assert.deepEqual(Object.keys(feature).sort(), ['CanonicalRole', 'RecentPrimary', 'VisibleByteBucket'])
  assert.equal(feature.VisibleByteBucket, 2)
  assert.equal([...feature.RecentPrimary].length, 3)

  let state = Predictor.StrengthPredictor_empty
  let [next, firstReadonly] = Predictor.StrengthPredictor_observeFirst(feature, S.ReadonlyBatch, state)
  assert.equal(firstReadonly, true)
  state = Predictor.StrengthPredictor_observeSecond(feature, S.ReadonlyBatch, next)

  ;[next, firstReadonly] = Predictor.StrengthPredictor_observeFirst(feature, S.TextOnly, state)
  assert.equal(firstReadonly, false)
  state = next

  const bucket = Predictor.StrengthPredictor_bucket(feature, state)
  assert.deepEqual(
    {
      opportunities: bucket.Opportunities,
      readonlyFirst: bucket.ReadonlyFirst,
      secondObservations: bucket.SecondObservations,
      readonlySecond: bucket.ReadonlySecond,
    },
    { opportunities: 2, readonlyFirst: 1, secondObservations: 1, readonlySecond: 1 },
  )

  const prediction = Predictor.StrengthPredictor_predict(feature, state)
  assert.equal(prediction.P1, 0.5)
  assert.equal(prediction.P2, 1)
  assert.equal(prediction.EvidenceCount, 1)
})

test('STRENGTH_010_control_assignment_is_restart_stable_and_has_no_predictor_score_input', () => {
  const first = Policy.StrengthPolicy_controlBucket(HostDigest.sha256Hex, 'policy-v1', 'root-1', 'run-1')
  const restart = Policy.StrengthPolicy_controlBucket(HostDigest.sha256Hex, 'policy-v1', 'root-1', 'run-1')
  const otherRun = Policy.StrengthPolicy_controlBucket(HostDigest.sha256Hex, 'policy-v1', 'root-1', 'run-2')

  assert.equal(first, restart)
  assert.notEqual(first, otherRun)
  assert.equal(Policy.StrengthPolicy_isControlHoldout(10000, first), true)
  assert.equal(Policy.StrengthPolicy_isControlHoldout(0, first), false)
})

test('STRENGTH_010_rollout_uses_explicit_costs_and_shadow_never_means_treatment', () => {
  const prediction = { P1: 0.75, P2: 0.5, EvidenceCount: 100 }
  const costs = {
    SavedDeep1: 10,
    SavedDeep2: 8,
    Fast1: 2,
    Fast2: 1,
    Byte1: 0.5,
    Byte2: 0.75,
    Delay1: 0.25,
    Delay2: 0.5,
    Risk1: 0.5,
    Risk2: 1,
  }

  const value = Rollout.StrengthRollout_estimate(prediction, costs)
  assert.ok(value.V1 > 0)
  assert.ok(Number.isFinite(value.V2))
  assert.equal(Rollout.StrengthRollout_isShadow(Rollout.StrengthRolloutMode.Shadow), true)
  assert.equal(Rollout.StrengthRollout_isShadow(Rollout.StrengthRolloutMode.Treatment), false)
  assert.equal(Rollout.StrengthRollout_isShadow(Rollout.StrengthRolloutMode.Off), false)
})
