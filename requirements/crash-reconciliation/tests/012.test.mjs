import assert from 'node:assert/strict'
import test from 'node:test'
import * as childWorkflow from '../../../dist/Execution/Delegation/Fork/ChildRecoveryWorkflowSurface.js'
import * as crashMatrix from '../../../dist/Execution/Delegation/Fork/JoinRecoveryCrashMatrixSurface.js'

test('WHAT[CRASH-012] VERIFY_008_child_recovery_workflow_commits_terminal_then_pulses_once_single_owner', () => {
  const res = childWorkflow.recoverTerminalPulse('ses-1', 'body-1')
  assert.equal(res.pulseCount, 1)
})

test('WHAT[CRASH-012] VERIFY_008_child_recovery_workflow_terminal_commit_single_owner_no_raw_publish_completion', () => {
  const res = childWorkflow.recoverTerminalSingleOwner('ses-1', 'body-1')
  assert.equal(res.rawPublish, false)
})

test('WHAT[CRASH-012] P0_RECOVERY_JOIN_001_crash_after_retired_is_idempotent', () => {
  const res = crashMatrix.scenario('crash-after-retired')
  assert.equal(res.idempotent, true)
})

test('WHAT[CRASH-012] P0_RECOVERY_JOIN_001_duplicate_retire_and_late_complete_are_absorbed', () => {
  const res = crashMatrix.scenario('late-complete-absorbed')
  assert.equal(res.absorbed, true)
})
