import assert from 'node:assert/strict'
import test from 'node:test'
import * as childWorkflow from '../../../dist/Execution/Delegation/Fork/ChildRecoveryWorkflowSurface.js'
import * as forkRestart from '../../../dist/Execution/Delegation/Fork/Host/HostForkRestartSurface.js'
import * as crashMatrix from '../../../dist/Execution/Delegation/Fork/JoinRecoveryCrashMatrixSurface.js'
import * as family from '../../../dist/Execution/Session/Recovery/SessionRecoveryFamilySurface.js'
import * as recoverySurface from '../../../dist/Execution/Session/Recovery/Surface.js'

test('WHAT[CRASH-005] VERIFY_008_child_recovery_workflow_waits_without_committing_when_snapshot_is_unreadable', () => {
  const res = childWorkflow.recoverUnreadable('ses-1')
  assert.equal(res.status, 'Waiting')
})

test('WHAT[CRASH-005] VERIFY_008_child_recovery_workflow_blocks_retired_handle', () => {
  const res = childWorkflow.recoverRetired('ses-1')
  assert.equal(res.status, 'Blocked')
})

test('WHAT[CRASH-005] VERIFY_008_child_recovery_workflow_incomplete_when_terminal_body_is_blank', () => {
  const res = childWorkflow.recoverBlank('ses-1')
  assert.equal(res.status, 'RecoveryIncomplete')
})

test('WHAT[CRASH-005] VERIFY_008_missing_ports_or_waiting_never_synthesizes_family_ready', () => {
  const res = childWorkflow.synthesizeFamilyReady(['Waiting'])
  assert.equal(res, false)
})

test('WHAT[CRASH-005] VERIFY_008_executor_tool_empty_or_whitespace_session_id_fails_closed', async () => {
  const res = await childWorkflow.execTool('   ')
  assert.equal(res.ok, false)
})

test('WHAT[CRASH-005] HFR_restart_active_with_unreadable_snapshot_waits_for_terminal_evidence', () => {
  const res = forkRestart.recoverActiveUnreadable('ses-1')
  assert.equal(res.status, 'Waiting')
})

test('WHAT[CRASH-005] P0_RECOVERY_JOIN_001_crash_before_handle_completed_append_has_no_completion', () => {
  const res = crashMatrix.scenario('crash-before-completed-append')
  assert.equal(res.hasCompletion, false)
})

test('WHAT[CRASH-005] RECOVERY_FAMILY_authorize_blocks_on_child_block', () => {
  const res = family.authorize(['Blocked'])
  assert.equal(res.status, 'Blocked')
})

test('WHAT[CRASH-005] RECOVERY_FAMILY_authorize_waiting_is_family_waiting_not_blocked', () => {
  const res = family.authorize(['Waiting'])
  assert.equal(res.status, 'Waiting')
})

test('WHAT[CRASH-005] RECOVERY_FAMILY_handle_family_waiting_maps_to_waiting_not_blocked', () => {
  const res = family.mapWaiting()
  assert.equal(res, 'Waiting')
})
