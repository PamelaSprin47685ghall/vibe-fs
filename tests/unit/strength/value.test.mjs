// tests/unit/Strength/value.test.mjs — spec/14 STRENGTH-024/025.
//
// The value function and the K selection rule. STRENGTH-024 names the exact
// decision rule: candidates start at {K0}; K1 joins when V1 ≥ threshold; K2
// joins only with an independent advantage over K1; ties pick the smaller K.

import assert from 'node:assert/strict'
import test from 'node:test'
import { strength } from '../support/domain.mjs'

// ── K selection rule (STRENGTH-024) ─────────────────────────────────────────

test('STRENGTH_024_below_threshold_always_selects_K0', () => {
  assert.equal(strength.chooseBudget(0, 0.01, 0.01), 'K0')
  assert.equal(strength.chooseBudget(0, 0.049, 0.049), 'K0')
})

test('STRENGTH_024_K1_selected_when_V1_passes_threshold_alone', () => {
  assert.equal(strength.chooseBudget(0, 0.10, 0.10), 'K1')
})

test('STRENGTH_024_K2_requires_an_independent_advantage_over_K1', () => {
  // V2 ≥ threshold but V2 - V1 < MinimumK2AdvantageOverK1 (0.20) → still K1.
  const result = strength.chooseBudget(0, 0.50, 0.55)
  assert.equal(result, 'K1')
})

test('STRENGTH_024_K2_selected_when_it_has_a_clear_advantage', () => {
  const result = strength.chooseBudget(0, 0.10, 0.40)
  assert.equal(result, 'K2')
})

test('STRENGTH_024_ties_pick_the_smaller_K', () => {
  assert.equal(strength.chooseBudget(0, 0.30, 0.30), 'K1')
})

// ── V1/V2 monotonicity ──────────────────────────────────────────────────────

test('STRENGTH_024_value_increases_with_read_probability', () => {
  const cost = strength.defaultCostModel('Fast')
  const low = strength.valueK1(cost, 0.3, 4096, 2.0)
  const high = strength.valueK1(cost, 0.9, 4096, 2.0)
  assert.ok(high > low, `higher P(read) should raise V1: ${high} vs ${low}`)
})

test('STRENGTH_024_value_decreases_with_projected_bytes', () => {
  const cost = strength.defaultCostModel('Fast')
  const small = strength.valueK1(cost, 0.5, 1024, 1.0)
  const large = strength.valueK1(cost, 0.5, 65536, 1.0)
  assert.ok(large < small, `more projected bytes should lower V1: ${large} vs ${small}`)
})

test('STRENGTH_024_deep_primary_raises_the_saved_value', () => {
  const fast = strength.defaultCostModel('Fast')
  const deep = strength.defaultCostModel('Deep')
  const fastValue = strength.valueK1(fast, 0.5, 4096, 1.0)
  const deepValue = strength.valueK1(deep, 0.5, 4096, 1.0)
  assert.ok(deepValue > fastValue, `a deep primary makes the same read more valuable`)
})

// ── byte contracts (STRENGTH-025) ───────────────────────────────────────────

test('STRENGTH_025_batch_byte_contract_rejects_oversized_batches', () => {
  assert.equal(strength.batchWithinByteLimit(1024), true)
  assert.equal(strength.batchWithinByteLimit(64 * 1024), true)
  assert.equal(strength.batchWithinByteLimit(64 * 1024 + 1), false)
})

test('STRENGTH_025_decision_byte_contract_caps_total', () => {
  assert.equal(strength.decisionWithinByteLimit(96 * 1024), true)
  assert.equal(strength.decisionWithinByteLimit(96 * 1024 + 1), false)
})
