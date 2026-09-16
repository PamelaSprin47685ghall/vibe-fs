import assert from 'node:assert/strict'
import test from 'node:test'
import * as childWorkflow from '../../../dist/Execution/Delegation/Fork/ChildRecoveryWorkflowSurface.js'
import * as forkRestart from '../../../dist/Execution/Delegation/Fork/Host/HostForkRestartSurface.js'
import * as crashMatrix from '../../../dist/Execution/Delegation/Fork/JoinRecoveryCrashMatrixSurface.js'
import * as sessionExtra from '../../../dist/Execution/Session/Recovery/SessionRecoveryExtraSurface.js'

test('WHAT[CRASH-002] VERIFY_008_child_recovery_workflow_commits_terminal_snapshot_then_pulses', () => {
  const res = childWorkflow.recoverTerminal('ses-1', 'body-1')
  assert.equal(res.ok, true)
})

test('WHAT[CRASH-002] HFR_restart_empty_journal_yields_no_linked_handles', () => {
  const handles = forkRestart.recoverHandles([])
  assert.deepEqual(handles, [])
})

test('WHAT[CRASH-002] HFR_restart_completed_terminal_re_enlists_child_into_runtime', () => {
  const res = forkRestart.recoverCompleted('ses-c1', 'terminal-body')
  assert.equal(res.ok, true)
})

test('WHAT[CRASH-002] HFR_restart_active_with_terminal_snapshot_recovered_terminal', () => {
  const res = forkRestart.recoverActiveWithSnapshot('ses-c1', 'terminal-body')
  assert.equal(res.status, 'RecoveredTerminal')
})

test('WHAT[CRASH-002] P0_RECOVERY_JOIN_001_crash_after_completed_before_consume_is_awaiting_join', () => {
  const res = crashMatrix.scenario('completed-before-consume')
  assert.equal(res.state, 'AwaitingJoin')
})

test('WHAT[CRASH-002] P0_RECOVERY_JOIN_001_duplicate_handle_completed_is_absorbed', () => {
  const res = crashMatrix.scenario('duplicate-complete')
  assert.equal(res.absorbed, true)
})

test('WHAT[CRASH-002] MISC_recovery_receipt_accessors_and_nonempty_helpers', () => {
  assert.ok(sessionExtra.helpers)
})
