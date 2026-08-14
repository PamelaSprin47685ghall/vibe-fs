// Split from tests/unit/process/process-output.test.mjs (cutover Wave 2a); owner: time-capability
//
// EXEC-011 deadline 代数：effectiveDeadline = min(estimate, hard limit)，
// nonfinite/nonpositive estimate 坍缩到 hard limit，DefaultHardLimit = 1h，
// outputThreshold 按 provider 意愿原样取值（collector/spool 断言 → process-execution）。

import assert from 'node:assert/strict'
import test from 'node:test'
import { lib } from '../../verification-system/tests/support/domain.mjs'

const {
  EstimatedRuntime,
  EstimatedOutput,
  ProcessEstimateModule_DefaultHardLimit,
  ProcessEstimateModule_effectiveDeadline,
  ProcessEstimateModule_outputThreshold,
} = await import('../../../dist/Process/ProcessRequest.js')

const { fromSeconds, fromHours, compare } = await lib('TimeSpan.js')

const runtime = (seconds) => new EstimatedRuntime(seconds)
const output = (bytes) => new EstimatedOutput(bytes)

// ── EXEC-011: estimate math ──────────────────────────────────────────────────

test('EXEC_011_output_threshold_uses_provider_willingness_at_face_value', () => {
  assert.equal(ProcessEstimateModule_outputThreshold(output(0n)), 0n)
  assert.equal(ProcessEstimateModule_outputThreshold(output(-5n)), 0n)
  assert.equal(ProcessEstimateModule_outputThreshold(output(10n)), 10n)
})

test('EXEC_011_effective_deadline_is_min_of_estimate_and_hard_limit', () => {
  const oneHour = fromHours(1)
  assert.equal(compare(ProcessEstimateModule_effectiveDeadline(runtime(10), oneHour), fromSeconds(10)), 0)
  assert.equal(compare(ProcessEstimateModule_effectiveDeadline(runtime(100), oneHour), fromSeconds(100)), 0)
  assert.equal(compare(ProcessEstimateModule_effectiveDeadline(runtime(2000), oneHour), fromSeconds(2000)), 0)
  assert.equal(compare(ProcessEstimateModule_effectiveDeadline(runtime(5000), oneHour), oneHour), 0)
})

test('EXEC_011_nonfinite_or_nonpositive_estimate_collapses_to_hard_limit', () => {
  const hard = fromSeconds(60)
  for (const bad of [NaN, Infinity, -Infinity, 0, -10]) {
    assert.equal(compare(ProcessEstimateModule_effectiveDeadline(runtime(bad), hard), hard), 0, String(bad))
  }
})

test('EXEC_011_default_hard_limit_is_one_hour', () => {
  assert.equal(compare(ProcessEstimateModule_DefaultHardLimit, fromHours(1)), 0)
})
