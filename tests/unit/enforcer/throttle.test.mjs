// tests/unit/Enforcer/throttle.test.mjs — spec/15 ENFORCER-080…090.
//
// The leaky-integrator throttle. ENFORCER-190 pure tests 8-13:
//   8. throttle is monotone in each s_i;
//   9. throttle is monotone in t for fixed evidence;
//   10. a single old report does not revive with age;
//   11. any fixed positive score sustained eventually triggers;
//   12. trigger resets;
//   13. state is empty after an epoch switch.

import assert from 'node:assert/strict'
import test from 'node:test'
import { enforcer } from '../support/domain.mjs'

// A 9/9 immediately triggers: pressure = (1 + 0/4) * (e^-0.25 * 0 + 1) = 1.0.
test('ENFORCER_084_a_full_score_triggers_immediately', () => {
  const s = enforcer.epochStart(0)
  const { state, pressure } = enforcer.observe(s, 9, 1)
  assert.equal(enforcer.shouldTrigger(pressure), true)
})

// A single low score decays and never revives (ENFORCER-088).
test('ENFORCER_088_an_isolated_low_score_never_revives', () => {
  const s = enforcer.epochStart(0)
  const { state, pressure } = enforcer.observe(s, 1, 1)
  assert.equal(enforcer.shouldTrigger(pressure), false)

  // As time passes with no new reports, the pressure from that one low score
  // decays toward zero (the (1 + t/τ) growth cannot outrun e^{-t/τ} decay for
  // a single observation far below threshold).
  const pressureLater = enforcer.isolatedPressure(1 / 9, 1000)
  assert.ok(pressureLater < 0.1, `isolated score must decay, pressure=${pressureLater}`)
})

// Sustained low scores eventually trigger (ENFORCER-087).
test('ENFORCER_087_sustained_low_scores_eventually_trigger', () => {
  let s = enforcer.epochStart(0)
  let triggered = false
  for (let n = 1; n <= 100; n++) {
    const { state, pressure } = enforcer.observe(s, 1, n)
    s = state
    if (enforcer.shouldTrigger(pressure)) {
      triggered = true
      break
    }
  }
  assert.equal(triggered, true, 'a sustained score of 1 must eventually trigger')
})

// Monotone in each score: a higher score at the same ordinal gives a higher
// (or equal) pressure.
test('ENFORCER_086_pressure_is_monotone_in_the_score', () => {
  for (let ordinal = 1; ordinal <= 10; ordinal++) {
    const low = enforcer.epochStart(0)
    const high = enforcer.epochStart(0)
    const { pressure: pLow } = enforcer.observe(low, 3, ordinal)
    const { pressure: pHigh } = enforcer.observe(high, 5, ordinal)
    assert.ok(pHigh >= pLow, `pressure should be monotone in score at ordinal ${ordinal}`)
  }
})

// Monotone in t for fixed evidence: same report history, later consumption
// means higher pressure.
test('ENFORCER_086_pressure_is_monotone_in_time_since_consumption', () => {
  const evidence = 0.5 // fixed accumulated evidence
  const early = enforcer.pressureAt(evidence, 1)
  const late = enforcer.pressureAt(evidence, 20)
  assert.ok(late > early, 'later consumption should give higher pressure for fixed evidence')
})

// Trigger resets the accumulator (ENFORCER-085).
test('ENFORCER_085_consume_resets_evidence', () => {
  let s = enforcer.epochStart(0)
  let { state } = enforcer.observe(s, 8, 1)
  s = state
  assert.ok(s.Evidence > 0)
  const consumed = enforcer.consume(s, 1)
  assert.equal(consumed.Evidence, 0)
  assert.equal(consumed.LastTriggerOrdinal, 1n)
})

// Epoch start is a virtual zero-evidence trigger (ENFORCER-081): first and
// later triggers use the same formula, no NeverIssued special case.
test('ENFORCER_081_epoch_start_serves_as_a_virtual_consumption', () => {
  const s = enforcer.epochStart(42)
  assert.equal(s.Evidence, 0)
  assert.equal(s.LastTriggerOrdinal, 42n)
})

// Replayability: the same score sequence gives the same pressure sequence.
test('ENFORCER_080_throttle_is_replayable', () => {
  const run = () => {
    let s = enforcer.epochStart(0)
    const pressures = []
    for (let n = 1; n <= 8; n++) {
      const { state, pressure } = enforcer.observe(s, n % 5, n)
      s = state
      pressures.push(pressure)
    }
    return pressures
  }
  assert.deepEqual(run(), run())
})
