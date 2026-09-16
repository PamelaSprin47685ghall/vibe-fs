import assert from 'node:assert/strict'
import test from 'node:test'
import * as companionRetry from '../../../dist/Context/Companion/CompanionRetryPolicySurface.js'

test('WHAT[CONTEXT-COMPRESSION-022] CTX_022_retry_sequence_returns_to_main_through_one_formula', () => {
  assert.ok(companionRetry.returnsThroughOneFormula)
})

test('WHAT[CONTEXT-COMPRESSION-022] CTX_022_durable_projection_keeps_newest_failure_count', () => {
  assert.ok(companionRetry.keepsNewestFailureCount)
})

test('WHAT[CONTEXT-COMPRESSION-022] CTX_022_projection_retry_follows_the_failure_budget', () => {
  assert.ok(companionRetry.followsFailureBudget)
})
