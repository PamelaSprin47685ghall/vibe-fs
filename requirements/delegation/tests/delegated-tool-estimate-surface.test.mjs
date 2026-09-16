// DELEG-022 estimate projection crosses the registered owner surface as plain data.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as estimate from '../../../dist/Execution/Delegation/DelegatedToolEstimateSurface.js'

test('WHAT[DELEG-022] P9_ESTIMATE_SURFACE_state_is_js_native_data', () => {
  const state = estimate.replay(3, [])
  assert.equal(Object.getPrototypeOf(state), Object.prototype)
  assert.deepEqual(state, { remaining: 3, countedCalls: 0 })
})

test('WHAT[DELEG-022] DELEG_022_replace_sets_exact_remaining_and_clears_prior_counted_calls', () => {
  assert.deepEqual(estimate.replay(3, ['call-1', 'call-2']), { remaining: 1, countedCalls: 2 })
  assert.deepEqual(estimate.replay(7, []), { remaining: 7, countedCalls: 0 })
})

test('WHAT[DELEG-022] DELEG_022_each_distinct_real_tool_call_decrements_once_and_saturates_at_zero', () => {
  assert.deepEqual(estimate.replay(2, ['call-1', 'call-1']), { remaining: 1, countedCalls: 1 })
  assert.deepEqual(estimate.replay(2, ['call-1', 'call-1', 'call-2']), { remaining: 0, countedCalls: 2 })
  assert.deepEqual(estimate.replay(2, ['call-1', 'call-1', 'call-2', 'call-3']), { remaining: 0, countedCalls: 2 })
})

test('WHAT[DELEG-022] DELEG_022_projection_is_incremental_and_deduplicates_calls', () => {
  // Incremental behavior: duplicate tool calls do not double decrement;
  // different tool calls decrement each time until saturation at zero.
  const state = estimate.replay(5, ['call-a', 'call-b', 'call-a', 'call-c', 'call-b'])
  assert.deepEqual(state, { remaining: 2, countedCalls: 3 })
  
  const saturated = estimate.replay(2, ['call-1', 'call-2', 'call-3', 'call-4'])
  assert.deepEqual(saturated, { remaining: 0, countedCalls: 2 })
})
