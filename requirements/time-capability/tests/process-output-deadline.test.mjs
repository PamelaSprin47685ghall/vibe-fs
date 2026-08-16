// TIME-002 — process estimate deadlines and output budgets stay bounded.

import assert from 'node:assert/strict'
import test from 'node:test'

const process = await import('../../../dist/Process/Surface.js')

test('WHAT[TIME-002] EXEC_011_output_threshold_uses_provider_willingness_at_face_value', () => {
  assert.equal(process.outputThreshold(0), 0)
  assert.equal(process.outputThreshold(-5), 0)
  assert.equal(process.outputThreshold(10), 10)
})

test('WHAT[TIME-002] EXEC_011_effective_deadline_is_min_of_estimate_and_hard_limit', () => {
  assert.equal(process.effectiveDeadlineSeconds(10, 3600), 10)
  assert.equal(process.effectiveDeadlineSeconds(100, 3600), 100)
  assert.equal(process.effectiveDeadlineSeconds(2000, 3600), 2000)
  assert.equal(process.effectiveDeadlineSeconds(5000, 3600), 3600)
})

test('WHAT[TIME-002] EXEC_011_nonfinite_or_nonpositive_estimate_collapses_to_hard_limit', () => {
  for (const bad of [NaN, Infinity, -Infinity, 0, -10]) {
    assert.equal(process.effectiveDeadlineSeconds(bad, 60), 60, String(bad))
  }
})

test('WHAT[TIME-002] EXEC_011_default_hard_limit_is_one_hour', () => {
  assert.equal(process.defaultHardLimitSeconds, 3600)
})
