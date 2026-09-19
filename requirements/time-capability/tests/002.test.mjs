import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { assertOpaque } = await import("../../verification-system/tests/support/js-contract.mjs");

const deadline = await import('../../../dist/Process/DeadlineSurface.js')
const ISO_START = '2026-01-01T00:00:00Z'

test('WHAT[time-capability-002] TIME_002_deadline_of_budget_and_remaining_are_pure_clock_functions', () => {
  const dl = deadline.create(ISO_START, 5000)
  assertOpaque(dl, 'deadline')

  assert.equal(deadline.remainingMs('2026-01-01T00:00:02Z', dl), 3000)
  assert.equal(deadline.isExpired('2026-01-01T00:00:02Z', dl), false)
  assert.equal(deadline.isExpired('2026-01-01T00:00:05Z', dl), true)
  assert.equal(deadline.remainingMs('2026-01-01T00:00:06Z', dl), 0)
})
test('WHAT[time-capability-002] TIME_002_of_budget_clamps_to_datetime_max_no_overflow', () => {
  const dl = deadline.create(ISO_START, 1e15)
  assert.equal(deadline.isExpired('2099-01-01T00:00:00Z', dl), false)
  const remainingAtStart = deadline.remainingMs(ISO_START, dl)
  assert.ok(Number.isFinite(remainingAtStart))
  assert.ok(remainingAtStart > 0)
})
test('WHAT[time-capability-002] TIME_002_next_wait_ms_caps_at_js_timer_ceiling', () => {
  assert.equal(deadline.maxTimerWaitMs, 2147483647)

  const long = deadline.create(ISO_START, 1e15)
  assert.equal(deadline.nextWaitMs(ISO_START, long), 2147483647)

  const short = deadline.create(ISO_START, 5000)
  assert.equal(deadline.nextWaitMs('2026-01-01T00:00:01Z', short), 4000)
  assert.equal(deadline.nextWaitMs('2026-01-01T00:00:06Z', short), 0)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");

const process = await import('../../../dist/Process/Surface.js')

test('WHAT[time-capability-002] EXEC_025_join_deadline_expired_renders_waiting_ended_natural_language', () => {
  assert.match(process.renderDeadlineExpired(), /No return reached you before your waiting ended/)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");

const process = await import('../../../dist/Process/Surface.js')

test('WHAT[time-capability-002] EXEC_011_output_threshold_uses_provider_willingness_at_face_value', () => {
  assert.equal(process.outputThreshold(0), 0)
  assert.equal(process.outputThreshold(-5), 0)
  assert.equal(process.outputThreshold(10), 10)
})
test('WHAT[time-capability-002] EXEC_011_effective_deadline_is_min_of_estimate_and_hard_limit', () => {
  assert.equal(process.effectiveDeadlineSeconds(10, 3600), 10)
  assert.equal(process.effectiveDeadlineSeconds(100, 3600), 100)
  assert.equal(process.effectiveDeadlineSeconds(2000, 3600), 2000)
  assert.equal(process.effectiveDeadlineSeconds(5000, 3600), 3600)
})
test('WHAT[time-capability-002] EXEC_011_nonfinite_or_nonpositive_estimate_collapses_to_hard_limit', () => {
  for (const bad of [NaN, Infinity, -Infinity, 0, -10]) {
    assert.equal(process.effectiveDeadlineSeconds(bad, 60), 60, String(bad))
  }
})
test('WHAT[time-capability-002] EXEC_011_default_hard_limit_is_one_hour', () => {
  assert.equal(process.defaultHardLimitSeconds, 3600)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");

const process = await import('../../../dist/Process/Surface.js')

test('WHAT[time-capability-002] EXEC_011_rejects_nan_runtime_estimate', () => {
  const result = process.validateEstimate(NaN, 1024)
  assert.equal(result.ok, false)
  assert.match(result.error, /finite positive number/)
})
test('WHAT[time-capability-002] EXEC_011_rejects_zero_and_negative_runtime_estimate', () => {
  for (const bad of [0, -5, -Infinity, Infinity]) {
    const result = process.validateEstimate(bad, 1024)
    assert.equal(result.ok, false, String(bad))
    assert.match(result.error, /finite positive number/)
  }
})
test('WHAT[time-capability-002] EXEC_011_rejects_negative_output_estimate', () => {
  const result = process.validateEstimate(10, -1)
  assert.equal(result.ok, false)
  assert.match(result.error, /non-negative/)
})
}
