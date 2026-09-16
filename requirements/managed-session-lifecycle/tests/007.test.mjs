import assert from 'node:assert/strict'
import test from 'node:test'
import * as handle from '../../../dist/Execution/Session/HandleSurface.js'
import * as joinProp from '../../../dist/Execution/Session/JoinCompletionPropertySurface.js'
import * as joinGuard from '../../../dist/Execution/Session/JoinGuardSurface.js'

test('WHAT[MANAGED-SESSION-007] LOOP_optional_string_traversal_calls_extract_zero_for_None_once_for_Some', () => {
  assert.equal(handle.testOptionalStringTraversal(), true)
})

test('WHAT[MANAGED-SESSION-007] EXEC_004_the_first_completion_wins_and_later_ones_are_refused', () => {
  assert.equal(handle.testFirstCompletionWins(), true)
})

test('WHAT[MANAGED-SESSION-007] EXEC_004_each_completion_kind_survives_into_the_state', () => {
  assert.equal(handle.testCompletionKindsSurvive(), true)
})

test('WHAT[MANAGED-SESSION-007] EXEC_004_completing_an_unknown_handle_is_refused_by_name', () => {
  assert.equal(handle.testCompletingUnknownRefused(), true)
})

test('WHAT[MANAGED-SESSION-007] EXEC_009_completed_awaiting_join_carries_blob_refs', () => {
  assert.equal(handle.testCompletedCarriesBlobRefs(), true)
})

test('WHAT[MANAGED-SESSION-007] EXEC_009_cancelled_completion_has_no_blob', () => {
  assert.equal(handle.testCancelledNoBlob(), true)
})

test('WHAT[MANAGED-SESSION-007] EXEC_009_fold_replays_completion_blob_refs', () => {
  assert.equal(handle.testFoldReplaysBlobRefs(), true)
})

test('WHAT[MANAGED-SESSION-007] EXEC_009_codec_migrates_0_5_1_handle_completed_missing_blob_fields', () => {
  assert.equal(handle.testCodecMigratesMissingBlobFields(), true)
})

test('WHAT[MANAGED-SESSION-007] every completion race preserves the first production winner', () => {
  assert.equal(joinProp.testFirstWinnerPreserved(), true)
})

test('WHAT[MANAGED-SESSION-007] THEOREM_handle_completed_causally_awakens_joinable', () => {
  assert.equal(joinGuard.testHandleCompletedAwakens(), true)
})

test('WHAT[MANAGED-SESSION-007] THEOREM_join_wake_path_trace_WorkActivated_then_HandleCompleted', () => {
  assert.equal(joinGuard.testWakePathTrace(), true)
})
