import assert from 'node:assert/strict'
import test from 'node:test'
import * as ForkLifecycleSurface from '../../../dist/Execution/Delegation/Fork/LifecycleSurface.js'

const childRunSnapshot = ({ action = 'fresh', runtimeCancelled = false, message = 'done' } = {}) =>
  ForkLifecycleSurface.snapshot(action, runtimeCancelled, message)

test('WHAT[MANAGED-SESSION-012] VERIFY_009_child_run_starts_active', () => {
  const run = ForkLifecycleSurface.snapshot('fresh', false, 'done')
  assert.equal(run.active, true)
  assert.equal(run.completed, false)
  assert.equal(run.cancelled, false)
})

test('WHAT[MANAGED-SESSION-012] VERIFY_009_child_run_cancel_flips_active_and_cancelled', () => {
  const run = childRunSnapshot({ action: 'cancel' })
  assert.equal(run.active, false)
  assert.equal(run.cancelled, true)
  assert.equal(run.completed, false)
})

test('WHAT[MANAGED-SESSION-012] VERIFY_009_child_run_bind_session_records_child_session', () => {
  const run = { ...childRunSnapshot(), childSession: 'ses-child-9' }
  assert.equal(run.childSession, 'ses-child-9')
})

test('WHAT[MANAGED-SESSION-012] VERIFY_009_child_run_completion_cell_is_single_assignment', () => {
  const run = childRunSnapshot({ action: 'complete' })
  assert.equal(run.completionCellSettled, true)
  assert.equal(run.status, 'Idle')
  assert.equal(run.completed, true)
})

test('WHAT[MANAGED-SESSION-012] VERIFY_009_child_run_make_failed_carries_error_outcome', () => {
  const run = childRunSnapshot({ action: 'fail', message: 'boom' })
  assert.equal(run.completionCellSettled, true)
  assert.equal(run.terminalStatusLabel, 'boom')
})

test('WHAT[MANAGED-SESSION-012] VERIFY_009_projection_status_busy_while_running', () => {
  assert.equal(childRunSnapshot().status, 'Busy')
})

test('WHAT[MANAGED-SESSION-012] VERIFY_009_projection_status_closed_on_cancel_or_runtime_cancel', () => {
  assert.equal(childRunSnapshot({ action: 'cancel' }).status, 'Closed')
  assert.equal(childRunSnapshot({ runtimeCancelled: true }).status, 'Closed')
})

test('WHAT[MANAGED-SESSION-012] VERIFY_009_projection_status_interrupted_on_interrupt_code', () => {
  assert.equal(childRunSnapshot({ action: 'interrupt', message: 'interrupted by user' }).status, 'Interrupted')
})

test('WHAT[MANAGED-SESSION-012] VERIFY_009_projection_status_closed_on_abandon', () => {
  assert.equal(childRunSnapshot({ action: 'abandon', message: 'gave up' }).status, 'Closed')
})

test('WHAT[MANAGED-SESSION-012] VERIFY_009_projection_status_idle_on_clean_completion', () => {
  assert.equal(childRunSnapshot({ action: 'complete' }).status, 'Idle')
})

test('WHAT[MANAGED-SESSION-012] VERIFY_009_projection_status_idle_for_other_failures', () => {
  assert.equal(childRunSnapshot({ action: 'fail', message: 'too slow' }).status, 'Idle')
})

test('WHAT[MANAGED-SESSION-012] VERIFY_009_projection_to_record_running_state', () => {
  const record = childRunSnapshot()
  assert.equal(record.agentId, 'agent-1')
  assert.equal(record.agent, 'coder')
  assert.equal(record.role, 'Manager')
  assert.equal(record.status, 'Busy')
  assert.equal(record.currentRunId, 'run-1')
  assert.equal(record.terminalStatusLabel, null)
  assert.equal(record.completionCellSettled, false)
})

test('WHAT[MANAGED-SESSION-012] VERIFY_009_projection_to_record_interrupted_label_is_message', () => {
  const record = childRunSnapshot({ action: 'interrupt', message: 'stop now' })
  assert.equal(record.terminalStatusLabel, 'stop now')
  assert.equal(record.status, 'Interrupted')
})

test('WHAT[MANAGED-SESSION-012] VERIFY_009_projection_to_record_abandoned_label_is_reason', () => {
  const record = childRunSnapshot({ action: 'abandon', message: 'no longer joinable' })
  assert.equal(record.terminalStatusLabel, 'no longer joinable')
  assert.equal(record.status, 'Closed')
})

test('WHAT[MANAGED-SESSION-012] VERIFY_009_projection_to_record_completed_label_is_status_text', () => {
  const record = childRunSnapshot({ action: 'complete' })
  assert.equal(record.terminalStatusLabel, 'completed')
  assert.equal(record.status, 'Idle')
})
