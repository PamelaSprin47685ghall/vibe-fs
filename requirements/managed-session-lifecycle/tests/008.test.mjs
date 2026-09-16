import assert from 'node:assert/strict'
import test from 'node:test'
import * as handle from '../../../dist/Execution/Session/HandleSurface.js'
import * as joinGuard from '../../../dist/Execution/Session/JoinGuardSurface.js'
import * as abandonedOrder from '../../../dist/Execution/Session/AbandonedOrderLifecycleSurface.js'
import * as foldSurface from '../../../dist/Execution/Delegation/Handle/FoldSurface.js'

test('WHAT[MANAGED-SESSION-008] EXEC_004_join_may_only_retire_a_handle_that_actually_completed', () => {
  assert.equal(handle.testRetireOnlyCompleted(), true)
})

test('WHAT[MANAGED-SESSION-008] EXEC_009_a_replayed_completion_or_retirement_is_absorbed', () => {
  assert.equal(handle.testReplayedCompletionAbsorbed(), true)
})

test('WHAT[MANAGED-SESSION-008] EXEC_004_a_retirement_without_a_completion_stops_the_replay', () => {
  assert.equal(handle.testRetirementWithoutCompletionStopsReplay(), true)
})

test('WHAT[MANAGED-SESSION-008] fold_refuses_unknown_fact_case', () => {
  assert.equal(handle.testFoldRefusesUnknownFactCase(), true)
})

test('WHAT[MANAGED-SESSION-008] THEOREM_blocked_to_awakened_fold_trails_confluent_after_retire', () => {
  assert.equal(joinGuard.testTrailsConfluentAfterRetire(), true)
})

test('WHAT[MANAGED-SESSION-008] EXEC_009_consume_abandoned_writes_HandleRetired_second_AlreadyRetired', () => {
  assert.equal(abandonedOrder.testConsumeAbandonedWritesRetired(), true)
})
