import assert from 'node:assert/strict'
import test from 'node:test'
import * as abandoned from '../../../dist/Execution/Session/HandleAbandonedSurface.js'
import * as handle from '../../../dist/Execution/Session/HandleSurface.js'
import * as abandonedOrder from '../../../dist/Execution/Session/AbandonedOrderLifecycleSurface.js'
import * as shutdown from '../../../dist/Execution/Session/ShutdownDrainContractSurface.js'
import * as syncDelegate from '../../../dist/Execution/Session/SyncDelegateLifecycleSurface.js'
import * as journalSurface from '../../../dist/Execution/Delegation/Handle/JournalSurface.js'

test('WHAT[MANAGED-SESSION-009] EXEC_009_HandleAbandoned_serializes_round_trip', () => {
  assert.equal(abandoned.testRoundTrip(), true)
})

test('WHAT[MANAGED-SESSION-009] EXEC_009_Active_to_Abandoned_fold_and_projection', () => {
  assert.equal(abandoned.testActiveToAbandoned(), true)
})

test('WHAT[MANAGED-SESSION-009] EXEC_009_CompletedAwaitingJoin_can_abandon', () => {
  assert.equal(abandoned.testCompletedCanAbandon(), true)
})

test('WHAT[MANAGED-SESSION-009] EXEC_009_Abandoned_is_not_joinable_and_cannot_complete', () => {
  assert.equal(abandoned.testAbandonedNotJoinable(), true)
})

test('WHAT[MANAGED-SESSION-009] EXEC_009_recordAbandon_CAS_first_wins', () => {
  assert.equal(abandoned.testRecordAbandonCas(), true)
})

test('WHAT[MANAGED-SESSION-009] EXEC_009_fold_replays_HandleAbandoned_idempotent', () => {
  assert.equal(abandoned.testFoldReplaysIdempotent(), true)
})

test('WHAT[MANAGED-SESSION-009] EXEC_009_retire_tombstone_unaffected_by_abandon_path', () => {
  assert.equal(abandoned.testRetireTombstoneUnaffected(), true)
})

test('WHAT[MANAGED-SESSION-009] EXEC_009_projection_CAS_duplicate_abandon_refused', () => {
  assert.equal(abandoned.testDuplicateAbandonRefused(), true)
})

test('WHAT[MANAGED-SESSION-009] EXEC_009_parent_abort_needs_the_handles_themselves_not_a_count', () => {
  assert.equal(handle.testParentAbortNeedsHandles(), true)
})

test('WHAT[MANAGED-SESSION-009] EXEC_009_abandoned_retire_clears_reportable_single_report', () => {
  assert.equal(abandonedOrder.testAbandonedRetireClearsReportable(), true)
})

test('WHAT[MANAGED-SESSION-009] provider transform is admitted into plugin shutdown ownership', () => {
  assert.equal(shutdown.testProviderTransformShutdown(), true)
})

test('WHAT[MANAGED-SESSION-009] G2_inspector_cancel_owner_fails_pending_invoke_no_extra_child', async () => {
  const r = await syncDelegate.testInspectorCancelFailsPending()
  assert.equal(r.ok, true)
})
