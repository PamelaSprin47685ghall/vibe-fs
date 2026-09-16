import assert from 'node:assert/strict'
import test from 'node:test'
import * as handle from '../../../dist/Execution/Session/HandleSurface.js'
import * as abandonedOrder from '../../../dist/Execution/Session/AbandonedOrderLifecycleSurface.js'
import * as tp from '../../../dist/OpenCode/Host/TerminalPolicySurface.js'

test('WHAT[MANAGED-SESSION-015] EXEC_009_only_an_agent_handle_answers_the_agent_question', () => {
  assert.equal(handle.testOnlyAgentHandleAnswers(), true)
})

test('WHAT[MANAGED-SESSION-015] EXEC_009_a_linked_handle_records_the_child_session_it_drives', () => {
  assert.equal(handle.testLinkedHandleRecordsChild(), true)
})

test('WHAT[MANAGED-SESSION-015] EXEC_009_replaying_the_exact_live_link_is_idempotent', () => {
  assert.equal(handle.testReplayLiveLinkIdempotent(), true)
})

test('WHAT[MANAGED-SESSION-015] EXEC_009_one_durable_handle_cannot_be_rebound_to_another_child', () => {
  assert.equal(handle.testHandleCannotBeRebound(), true)
})

test('WHAT[MANAGED-SESSION-015] EXEC_009_a_completion_for_a_handle_that_was_never_linked_stops_the_replay', () => {
  assert.equal(handle.testUnlinkedCompletionStopsReplay(), true)
})

test('WHAT[MANAGED-SESSION-015] EXEC_018_creation_order_follows_HandleLinked_fold_sequence', () => {
  assert.equal(abandonedOrder.testCreationOrderFollowsLinkedSequence(), true)
})

test('WHAT[MANAGED-SESSION-015] TPOL_linked_child_keeps_exact_handle_and_target', () => {
  assert.equal(tp.testLinkedChildKeepsExactHandle(), true)
})
