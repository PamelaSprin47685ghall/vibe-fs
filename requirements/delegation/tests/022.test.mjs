import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const estimate = await import("../../../dist/Execution/Delegation/DelegatedToolEstimateSurface.js");


test('WHAT[delegation-022] DELEG_022_durable_replace_and_tool_observation_fold_incrementally', () => {
  assert.deepEqual(estimate.replay(3, []), { remaining: 3, countedCalls: 0 })
  assert.deepEqual(estimate.replay(3, ['tool-1']), { remaining: 2, countedCalls: 1 })
  assert.deepEqual(estimate.replay(3, ['tool-1', 'tool-1']), { remaining: 2, countedCalls: 1 })
})
test('WHAT[delegation-022] DELEG_022_durable_replace_resets_measurement_without_program_stage', () => {
  assert.deepEqual(estimate.replay(5, []), { remaining: 5, countedCalls: 0 })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const estimate = await import("../../../dist/Execution/Delegation/DelegatedToolEstimateSurface.js");


test('WHAT[delegation-022] P9_ESTIMATE_SURFACE_state_is_js_native_data', () => {
  const state = estimate.replay(3, [])
  assert.equal(Object.getPrototypeOf(state), Object.prototype)
  assert.deepEqual(state, { remaining: 3, countedCalls: 0 })
})
test('WHAT[delegation-022] DELEG_022_replace_sets_exact_remaining_and_clears_prior_counted_calls', () => {
  assert.deepEqual(estimate.replay(3, ['call-1', 'call-2']), { remaining: 1, countedCalls: 2 })
  assert.deepEqual(estimate.replay(7, []), { remaining: 7, countedCalls: 0 })
})
test('WHAT[delegation-022] DELEG_022_each_distinct_real_tool_call_decrements_once_and_saturates_at_zero', () => {
  assert.deepEqual(estimate.replay(2, ['call-1', 'call-1']), { remaining: 1, countedCalls: 1 })
  assert.deepEqual(estimate.replay(2, ['call-1', 'call-1', 'call-2']), { remaining: 0, countedCalls: 2 })
  assert.deepEqual(estimate.replay(2, ['call-1', 'call-1', 'call-2', 'call-3']), { remaining: 0, countedCalls: 2 })
})
test('WHAT[delegation-022] DELEG_022_projection_is_incremental_and_deduplicates_calls', () => {
  // Incremental behavior: duplicate tool calls do not double decrement;
  // different tool calls decrement each time until saturation at zero.
  const state = estimate.replay(5, ['call-a', 'call-b', 'call-a', 'call-c', 'call-b'])
  assert.deepEqual(state, { remaining: 2, countedCalls: 3 })
  
  const saturated = estimate.replay(2, ['call-1', 'call-2', 'call-3', 'call-4'])
  assert.deepEqual(saturated, { remaining: 0, countedCalls: 2 })
})
}
