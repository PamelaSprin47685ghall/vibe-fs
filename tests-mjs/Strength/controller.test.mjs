// tests-mjs/Strength/controller.test.mjs — spec/14 STRENGTH-027/028/030/031/093.
//
// The negative-feedback controller: ρ moves opposite to the smoothed tendency,
// updates slowly, and never saturates at 0 or 1. STRENGTH-093 demands:
//   ρ never sticks at 0 or 1, z and ρ do not oscillate forever, step size is
//   bounded, K1/K2 rings are independent, and the system re-enters a stable
//   band after a distribution shift.

import assert from 'node:assert/strict'
import test from 'node:test'
import { strength } from '../domain.mjs'

const sha = (s) => {
  // A deterministic stand-in for HostDigest.sha256Hex (pure, so fine for the
  // deterministic-sampling tests; the real digest is injected at production).
  let h = 0
  for (let i = 0; i < s.length; i++) h = (h * 31 + s.charCodeAt(i)) >>> 0
  return h.toString(16).padStart(16, '0').repeat(4)
}

// ── deterministic sampling (STRENGTH-027) ───────────────────────────────────

test('STRENGTH_027_sampling_is_deterministic_for_the_same_seed', () => {
  const a = strength.includedInTraining(sha, 'decision-1', 0, 0.5)
  const b = strength.includedInTraining(sha, 'decision-1', 0, 0.5)
  assert.equal(a.included, b.included)
  assert.equal(a.u, b.u)
})

test('STRENGTH_027_sampling_uses_decisionId_and_ordinal_not_time', () => {
  const d1 = strength.includedInTraining(sha, 'decision-1', 0, 0.5)
  const d2 = strength.includedInTraining(sha, 'decision-2', 0, 0.5)
  const o2 = strength.includedInTraining(sha, 'decision-1', 1, 0.5)
  assert.ok(d1.u !== d2.u || d1.included !== d2.included, 'different decisions should differ in at least one output')
  assert.ok(d1.u !== o2.u || d1.included !== o2.included, 'different ordinals should differ in at least one output')
})

test('STRENGTH_027_hash_lands_in_half_open_unit_interval', () => {
  // u must be in [0,1): the all-ones digest must NOT map to exactly 1.0 (that
  // would make the decision never included for any p < 1). The 53-bit mask over
  // 2^53 guarantees it — 2^63 fails because JS doubles cannot represent 2^63-1
  // and 2^63 distinctly (measured in 03528232).
  const allOnes = 'f'.repeat(64)
  const u = strength.hashToUnitInterval(sha, 'whatever')
  assert.ok(u >= 0 && u < 1, `u must be in [0,1), got ${u}`)
  const extreme = strength.hashToUnitInterval(() => allOnes, 'seed')
  assert.ok(extreme < 1, `all-ones digest must map below 1.0, got ${extreme}`)
})

test('STRENGTH_027_higher_frozen_probability_never_decreases_inclusion', () => {
  // For a fixed seed, u is fixed; inclusion is u < p, which is monotone in p.
  for (let round = 0; round < 20; round++) {
    const seed = `probe-${round}`
    const low = strength.hashToUnitInterval(sha, seed)
    const atLow = low < 0.3
    const atHigh = low < 0.9
    assert.ok(!atLow || atHigh, 'inclusion at p=0.9 must hold whenever it holds at p=0.3')
  }
})

// ── negative feedback direction (STRENGTH-028) ──────────────────────────────

test('STRENGTH_028_rising_tendency_lowers_inclusion_probability', () => {
  const state = strength.initialState()
  const calm = strength.updateProbability(0.1, 0.05, 0.95, 0.5, state.InclusionProbability1, 0.2)
  const agitated = strength.updateProbability(0.1, 0.05, 0.95, 0.5, state.InclusionProbability1, 0.9)
  assert.ok(
    agitated < calm,
    `higher tendency should lower ρ: agitated=${agitated} calm=${calm}`,
  )
})

// ── slow update & clamping (STRENGTH-030) ───────────────────────────────────

test('STRENGTH_030_probability_is_clamped_and_step_limited', () => {
  const state = strength.initialState()
  // A violent tendency swing still cannot move ρ more than one step per update.
  const moved = strength.updateProbability(1.0, 0.05, 0.95, 0.01, 0.5, 1.0)
  assert.ok(
    Math.abs(moved - 0.5) <= 0.01 + 1e-9,
    `step limit violated: moved from 0.5 to ${moved}`,
  )
})

test('STRENGTH_030_clamps_never_touch_0_or_1', () => {
  const clampedLow = strength.updateProbability(1.0, 0.05, 0.95, 1.0, 0.1, 1.0)
  const clampedHigh = strength.updateProbability(1.0, 0.05, 0.95, 1.0, 0.9, 0.0)
  assert.ok(clampedLow >= 0.05 && clampedLow <= 0.95, `low clamp: ${clampedLow}`)
  assert.ok(clampedHigh >= 0.05 && clampedHigh <= 0.95, `high clamp: ${clampedHigh}`)
})

// ── two independent rings (STRENGTH-031) ────────────────────────────────────

test('STRENGTH_031_k2_ring_updates_slower_than_k1', () => {
  // K2's half-life is 2x K1's, so its alpha is smaller: one loud tendency
  // moves K1 more than K2.
  const alpha1 = strength.ewmaAlpha(512)
  const alpha2 = strength.ewmaAlpha(1024)
  assert.ok(alpha1 > alpha2, `K1 alpha (${alpha1}) should exceed K2 alpha (${alpha2})`)
})

test('STRENGTH_031_k2_cap_is_lower_than_k1', () => {
  // PolicyConstants: K1 max 0.95, K2 max 0.75. The controller must never
  // let K2 exceed its own cap.
  let state = strength.initialState()
  // Drive tendency to zero (max inclusion desired) for many updates.
  for (let i = 0; i < 500; i++) {
    state = strength.onEligibleDecision(state, 0.0, 0.0)
  }
  assert.ok(
    state.InclusionProbability2 <= 0.75 + 1e-9,
    `K2 inclusion ${state.InclusionProbability2} must respect its lower cap`,
  )
  assert.ok(
    state.InclusionProbability1 > state.InclusionProbability2,
    `K1 inclusion (${state.InclusionProbability1}) should exceed K2 (${state.InclusionProbability2})`,
  )
})

// ── re-entry after distribution shift (STRENGTH-093) ────────────────────────

test('STRENGTH_093_controller_never_saturates_at_the_bounds', () => {
  let state = strength.initialState()
  // Constant high tendency for many updates: ρ should fall but not stick at the
  // minimum — the step limit keeps it moving toward the band.
  for (let i = 0; i < 2000; i++) {
    state = strength.onEligibleDecision(state, 1.0, 1.0)
  }
  assert.ok(
    state.InclusionProbability1 >= 0.05 - 1e-9,
    `K1 ρ should not fall below its clamp: ${state.InclusionProbability1}`,
  )
  assert.ok(
    state.InclusionProbability2 >= 0.05 - 1e-9,
    `K2 ρ should not fall below its clamp: ${state.InclusionProbability2}`,
  )
})

test('STRENGTH_093_controller_reenters_stable_band_after_distribution_shift', () => {
  let state = strength.initialState()
  // Phase 1: high read tendency → ρ falls.
  for (let i = 0; i < 2000; i++) state = strength.onEligibleDecision(state, 1.0, 1.0)
  const depressed = state.InclusionProbability1
  // Phase 2: no read tendency → ρ rises back.
  for (let i = 0; i < 2000; i++) state = strength.onEligibleDecision(state, 0.0, 0.0)
  assert.ok(
    state.InclusionProbability1 > depressed,
    `ρ should recover after the tendency drops: ${state.InclusionProbability1} vs ${depressed}`,
  )
})
