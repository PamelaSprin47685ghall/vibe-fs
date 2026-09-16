import assert from 'node:assert/strict'
import test from 'node:test'
import * as handle from '../../../dist/Execution/Session/HandleSurface.js'
import * as joinGuard from '../../../dist/Execution/Session/JoinGuardSurface.js'
import * as tp from '../../../dist/OpenCode/Host/TerminalPolicySurface.js'

test('WHAT[MANAGED-SESSION-006] EXEC_009_agent_pty_and_manager_job_handles_are_separate_identities', () => {
  assert.equal(handle.testDistinctIdentities(), true)
})

test('WHAT[MANAGED-SESSION-006] EXEC_005_the_views_partition_the_lifecycle_and_never_show_retired', () => {
  assert.equal(handle.testViewsPartitionLifecycle(), true)
})

test('WHAT[MANAGED-SESSION-006] EXEC_009_a_retired_handle_answers_retired_forever', () => {
  assert.equal(handle.testRetiredForever(), true)
})

test('WHAT[MANAGED-SESSION-006] EXEC_009_a_retired_id_is_distinguishable_from_one_that_never_existed', () => {
  assert.equal(handle.testRetiredDistinguishable(), true)
})

test('WHAT[MANAGED-SESSION-006] EXEC_009_a_retired_child_session_is_still_recognised_as_a_child', () => {
  assert.equal(handle.testRetiredChildRecognised(), true)
})

test('WHAT[MANAGED-SESSION-006] EXEC_009_linked_children_lists_every_child_ever_linked', () => {
  assert.equal(handle.testLinkedChildrenList(), true)
})

test('WHAT[MANAGED-SESSION-006] EXEC_009_the_three_facts_replay_into_the_terminal_state', () => {
  assert.equal(handle.testThreeFactsReplay(), true)
})

test('WHAT[MANAGED-SESSION-006] EXEC_001_fork_creates_a_child_run', () => {
  assert.equal(handle.testForkCreatesChildRun(), true)
})

test('WHAT[MANAGED-SESSION-006] EXEC_007_nudge_is_fire_and_forget', () => {
  assert.equal(handle.testNudgeFireAndForget(), true)
})

test('WHAT[MANAGED-SESSION-006] surface_refuses_unknown_role', () => {
  assert.equal(handle.testRefuseUnknownRole(), true)
})

test('WHAT[MANAGED-SESSION-006] surface_refuses_unknown_completion_kind', () => {
  assert.equal(handle.testRefuseUnknownCompletionKind(), true)
})

test('WHAT[MANAGED-SESSION-006] surface_refuses_unknown_abandon_reason', () => {
  assert.equal(handle.testRefuseUnknownAbandonReason(), true)
})

test('WHAT[MANAGED-SESSION-006] surface_refuses_unknown_ownership', () => {
  assert.equal(handle.testRefuseUnknownOwnership(), true)
})

test('WHAT[MANAGED-SESSION-006] surface_refuses_unknown_command_op', () => {
  assert.equal(handle.testRefuseUnknownCommandOp(), true)
})

test('WHAT[MANAGED-SESSION-006] EXEC_016_listable_handles_are_outstanding_for_manager', () => {
  assert.equal(joinGuard.testListableHandlesOutstanding(), true)
})

test('WHAT[MANAGED-SESSION-006] THEOREM_join_blocked_while_handle_active', () => {
  assert.equal(joinGuard.testJoinBlockedWhileActive(), true)
})

test('WHAT[MANAGED-SESSION-006] THEOREM_WorkActivated_and_HandleLinked_interleavings_stay_blocked', () => {
  assert.equal(joinGuard.testInterleavingsStayBlocked(), true)
})

test('WHAT[MANAGED-SESSION-006] THEOREM_projection_steps_enumerate_blocked_then_awakened_then_clear', () => {
  assert.equal(joinGuard.testProjectionSteps(), true)
})

test('WHAT[MANAGED-SESSION-006] TPOL_sessionDead_false_without_journal', () => {
  assert.equal(tp.sessionDeadWithoutJournal('ses-test'), false)
})

test('WHAT[MANAGED-SESSION-006] TPOL_outstanding_without_durable_work_is_role_closed', () => {
  assert.equal(tp.outstandingWithoutJournal('Manager', false, 'ses-test'), false)
})
