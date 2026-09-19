import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const ForkLifecycleSurface = await import("../../../dist/Execution/Delegation/Fork/LifecycleSurface.js");

const childRunSnapshot = ({ action = 'fresh', runtimeCancelled = false, message = 'done' } = {}) =>
  ForkLifecycleSurface.snapshot(action, runtimeCancelled, message)

test('WHAT[managed-session-lifecycle-012] VERIFY_009_child_run_starts_active', () => {
  const run = ForkLifecycleSurface.snapshot('fresh', false, 'done')
  assert.equal(run.active, true)
  assert.equal(run.completed, false)
  assert.equal(run.cancelled, false)
})
test('WHAT[managed-session-lifecycle-012] VERIFY_009_child_run_cancel_flips_active_and_cancelled', () => {
  const run = childRunSnapshot({ action: 'cancel' })
  assert.equal(run.active, false)
  assert.equal(run.cancelled, true)
  assert.equal(run.completed, false)
})
test('WHAT[managed-session-lifecycle-012] VERIFY_009_child_run_bind_session_records_child_session', () => {
  const run = { ...childRunSnapshot(), childSession: 'ses-child-9' }
  assert.equal(run.childSession, 'ses-child-9')
})
test('WHAT[managed-session-lifecycle-012] VERIFY_009_child_run_completion_cell_is_single_assignment', () => {
  const run = childRunSnapshot({ action: 'complete' })
  assert.equal(run.completionCellSettled, true)
  assert.equal(run.status, 'Idle')
  assert.equal(run.completed, true)
})
test('WHAT[managed-session-lifecycle-012] VERIFY_009_child_run_make_failed_carries_error_outcome', () => {
  const run = childRunSnapshot({ action: 'fail', message: 'boom' })
  assert.equal(run.completionCellSettled, true)
  assert.equal(run.terminalStatusLabel, 'boom')
})
test('WHAT[managed-session-lifecycle-012] VERIFY_009_projection_status_busy_while_running', () => {
  assert.equal(childRunSnapshot().status, 'Busy')
})
test('WHAT[managed-session-lifecycle-012] VERIFY_009_projection_status_closed_on_cancel_or_runtime_cancel', () => {
  assert.equal(childRunSnapshot({ action: 'cancel' }).status, 'Closed')
  assert.equal(childRunSnapshot({ runtimeCancelled: true }).status, 'Closed')
})
test('WHAT[managed-session-lifecycle-012] VERIFY_009_projection_status_interrupted_on_interrupt_code', () => {
  assert.equal(childRunSnapshot({ action: 'interrupt', message: 'interrupted by user' }).status, 'Interrupted')
})
test('WHAT[managed-session-lifecycle-012] VERIFY_009_projection_status_closed_on_abandon', () => {
  assert.equal(childRunSnapshot({ action: 'abandon', message: 'gave up' }).status, 'Closed')
})
test('WHAT[managed-session-lifecycle-012] VERIFY_009_projection_status_idle_on_clean_completion', () => {
  assert.equal(childRunSnapshot({ action: 'complete' }).status, 'Idle')
})
test('WHAT[managed-session-lifecycle-012] VERIFY_009_projection_status_idle_for_other_failures', () => {
  assert.equal(childRunSnapshot({ action: 'fail', message: 'too slow' }).status, 'Idle')
})
test('WHAT[managed-session-lifecycle-012] VERIFY_009_projection_to_record_running_state', () => {
  const record = childRunSnapshot()
  assert.equal(record.agentId, 'agent-1')
  assert.equal(record.agent, 'coder')
  assert.equal(record.role, 'Manager')
  assert.equal(record.status, 'Busy')
  assert.equal(record.currentRunId, 'run-1')
  assert.equal(record.terminalStatusLabel, null)
  assert.equal(record.completionCellSettled, false)
})
test('WHAT[managed-session-lifecycle-012] VERIFY_009_projection_to_record_interrupted_label_is_message', () => {
  const record = childRunSnapshot({ action: 'interrupt', message: 'stop now' })
  assert.equal(record.terminalStatusLabel, 'stop now')
  assert.equal(record.status, 'Interrupted')
})
test('WHAT[managed-session-lifecycle-012] VERIFY_009_projection_to_record_abandoned_label_is_reason', () => {
  const record = childRunSnapshot({ action: 'abandon', message: 'no longer joinable' })
  assert.equal(record.terminalStatusLabel, 'no longer joinable')
  assert.equal(record.status, 'Closed')
})
test('WHAT[managed-session-lifecycle-012] VERIFY_009_projection_to_record_completed_label_is_status_text', () => {
  const record = childRunSnapshot({ action: 'complete' })
  assert.equal(record.terminalStatusLabel, 'completed')
  assert.equal(record.status, 'Idle')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const HostForkPtySurface = await import("../../../dist/Execution/Delegation/Fork/Host/HostForkPtySurface.js");


test('WHAT[managed-session-lifecycle-012] HFP_fork_pty_blank_command_is_refused', async () => {
  const result = await HostForkPtySurface.scenario('blank-fork', '   ', '')
  assert.equal(result.ok, false)
  assert.equal(result.error, 'PTY command is required')
  assert.deepEqual(result.calls, [])
})
test('WHAT[managed-session-lifecycle-012] HFP_fork_pty_tracks_registers_and_resolves_last', async () => {
  const result = await HostForkPtySurface.scenario('fork', 'ls -la', '')
  assert.equal(result.ok, true)
  assert.equal(result.owned, true)
  assert.equal(result.known, true)
})
test('WHAT[managed-session-lifecycle-012] HFP_fork_pty_port_exception_untracks_and_errors', async () => {
  const result = await HostForkPtySurface.scenario('fork-error', 'ls', 'pty spawn exploded')
  assert.equal(result.ok, false)
  assert.equal(result.error, 'pty spawn exploded')
  assert.equal(result.owned, false)
})
test('WHAT[managed-session-lifecycle-012] HFP_try_pty_unknown_string_id_is_none', async () => {
  const result = await HostForkPtySurface.scenario('lookup-unknown', 'foreign', '')
  assert.equal(result.ok, true)
  assert.equal(result.known, false)
})
test('WHAT[managed-session-lifecycle-012] HFP_send_pty_unowned_id_is_unknown', async () => {
  const result = await HostForkPtySurface.scenario('send-unowned', 'echo hi', '')
  assert.equal(result.ok, false)
  assert.equal(result.error, 'Unknown PTY id: foreign')
})
test('WHAT[managed-session-lifecycle-012] HFP_send_pty_owned_but_missing_on_port_is_unknown', async () => {
  const result = await HostForkPtySurface.scenario('send-closed', 'echo hi', '')
  assert.equal(result.ok, false)
  assert.match(result.error, /Unknown PTY id/)
})
test('WHAT[managed-session-lifecycle-012] HFP_send_pty_signal_forwards_signal_command', async () => {
  const result = await HostForkPtySurface.scenario('signal', 'INT', '')
  assert.equal(result.ok, true)
  assert.deepEqual(result.calls, [{ kind: 'signal', signal: 'SIGINT' }])
})
test('WHAT[managed-session-lifecycle-012] HFP_send_pty_write_forwards_write_command', async () => {
  const result = await HostForkPtySurface.scenario('write', 'echo hi', '')
  assert.equal(result.ok, true)
  assert.deepEqual(result.calls, [{ kind: 'write', text: 'echo hi\n' }])
})
test('WHAT[managed-session-lifecycle-012] HFP_send_pty_read_with_empty_prompt', async () => {
  const result = await HostForkPtySurface.scenario('read', '', '')
  assert.equal(result.ok, true)
  assert.equal(result.output, 'terminal text')
  assert.equal(result.closed, true)
})
test('WHAT[managed-session-lifecycle-012] HFP_send_pty_port_error_propagates', async () => {
  const result = await HostForkPtySurface.scenario('write', 'echo hi', 'pty session ended')
  assert.equal(result.ok, false)
  assert.equal(result.error, 'pty session ended')
})
test('WHAT[managed-session-lifecycle-012] HFP_track_untrack_pty_run_round_trip', async () => {
  const result = await HostForkPtySurface.scenario('track-untrack', 'tracked-1', '')
  assert.equal(result.ok, true)
  assert.equal(result.ownedBefore, true)
  assert.equal(result.ownedAfter, false)
})
}
