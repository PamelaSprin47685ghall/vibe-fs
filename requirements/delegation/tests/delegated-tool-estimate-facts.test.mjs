// Durable tool estimate fold through the delegation owner surface.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as estimate from '../../../dist/Execution/Delegation/DelegatedToolEstimateSurface.js'

test('WHAT[DELEG-022] DELEG_022_durable_replace_and_tool_observation_fold_incrementally', () => {
  assert.deepEqual(estimate.replay(3, []), { remaining: 3, countedCalls: 0 })
  assert.deepEqual(estimate.replay(3, ['tool-1']), { remaining: 2, countedCalls: 1 })
  assert.deepEqual(estimate.replay(3, ['tool-1', 'tool-1']), { remaining: 2, countedCalls: 1 })
})
test('WHAT[DELEG-022] DELEG_022_durable_replace_resets_measurement_without_program_stage', () => {
  assert.deepEqual(estimate.replay(5, []), { remaining: 5, countedCalls: 0 })
})
