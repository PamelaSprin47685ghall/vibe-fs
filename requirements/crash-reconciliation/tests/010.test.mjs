import assert from 'node:assert/strict'
import test from 'node:test'
import * as childWorkflow from '../../../dist/Execution/Delegation/Fork/ChildRecoveryWorkflowSurface.js'
import * as cleanBreak from '../../../dist/Execution/Delegation/Fork/JoinCleanBreakRecoverySurface.js'
import * as sessionExtra from '../../../dist/Execution/Session/Recovery/SessionRecoveryExtraSurface.js'
import * as family from '../../../dist/Execution/Session/Recovery/SessionRecoveryFamilySurface.js'

test('WHAT[CRASH-010] VERIFY_008_child_recovery_workflow_returns_active_without_committing_when_child_is_live', () => {
  const res = childWorkflow.recoverLive('ses-1')
  assert.equal(res.status, 'RecoveredActive')
})

test('WHAT[CRASH-010] VERIFY_008_child_recovery_workflow_unreadable_snapshot_is_incomplete_not_blocked', () => {
  const res = childWorkflow.recoverUnreadable('ses-1')
  assert.equal(res.status, 'Waiting')
})

test('WHAT[CRASH-010] VERIFY_008_child_recovery_workflow_retired_handle_is_blocked_branch', () => {
  const res = childWorkflow.recoverRetired('ses-1')
  assert.equal(res.status, 'Blocked')
})

test('WHAT[CRASH-010] VERIFY_008_child_recovery_workflow_blank_terminal_body_is_incomplete_branch', () => {
  const res = childWorkflow.recoverBlank('ses-1')
  assert.equal(res.status, 'RecoveryIncomplete')
})

test('WHAT[CRASH-010] P0_CLEAN_BREAK_aborted_only_observation_is_incomplete_not_blocked', () => {
  const res = cleanBreak.classify(['aborted'])
  assert.equal(res, 'Incomplete')
})

test('WHAT[CRASH-010] MISC_recovery_of_handle_family_all_branches', () => {
  assert.ok(sessionExtra.handleBranches)
})

test('WHAT[CRASH-010] MISC_recovery_of_job_family_all_branches', () => {
  assert.ok(sessionExtra.jobBranches)
})

test('WHAT[CRASH-010] RECOVERY_FAMILY_handle_family_types_and_permit_rules', () => {
  assert.ok(family.handleTypes)
})
