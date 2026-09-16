import assert from 'node:assert/strict'
import test from 'node:test'
import * as childRun from '../../../dist/Execution/Session/ChildRunProjectionSurface.js'
import * as pty from '../../../dist/Execution/Session/HostForkPtySurface.js'
import * as lifecycleSurface from '../../../dist/Execution/Delegation/Fork/LifecycleSurface.js'

test('WHAT[MANAGED-SESSION-012] VERIFY_009_child_run_completion_cell_is_single_assignment', () => {
  assert.equal(childRun.testCompletionCellSingleAssignment(), true)
})

test('WHAT[MANAGED-SESSION-012] VERIFY_009_child_run_make_failed_carries_error_outcome', () => {
  assert.equal(childRun.testMakeFailedCarriesError(), true)
})

test('WHAT[MANAGED-SESSION-012] VERIFY_009_projection_status_busy_while_running', () => {
  assert.equal(childRun.testStatusBusy(), true)
})

test('WHAT[MANAGED-SESSION-012] VERIFY_009_projection_status_closed_on_cancel_or_runtime_cancel', () => {
  assert.equal(childRun.testStatusClosedOnCancel(), true)
})

test('WHAT[MANAGED-SESSION-012] VERIFY_009_projection_status_interrupted_on_interrupt_code', () => {
  assert.equal(childRun.testStatusInterrupted(), true)
})

test('WHAT[MANAGED-SESSION-012] VERIFY_009_projection_status_closed_on_abandon', () => {
  assert.equal(childRun.testStatusClosedOnAbandon(), true)
})

test('WHAT[MANAGED-SESSION-012] VERIFY_009_projection_status_idle_on_clean_completion', () => {
  assert.equal(childRun.testStatusIdleOnClean(), true)
})

test('WHAT[MANAGED-SESSION-012] VERIFY_009_projection_status_idle_for_other_failures', () => {
  assert.equal(childRun.testStatusIdleOnFailure(), true)
})

test('WHAT[MANAGED-SESSION-012] VERIFY_009_projection_to_record_running_state', () => {
  assert.equal(childRun.testToRecordRunning(), true)
})

test('WHAT[MANAGED-SESSION-012] VERIFY_009_projection_to_record_interrupted_label_is_message', () => {
  assert.equal(childRun.testToRecordInterruptedLabel(), true)
})

test('WHAT[MANAGED-SESSION-012] VERIFY_009_projection_to_record_abandoned_label_is_reason', () => {
  assert.equal(childRun.testToRecordAbandonedLabel(), true)
})

test('WHAT[MANAGED-SESSION-012] VERIFY_009_projection_to_record_completed_label_is_status_text', () => {
  assert.equal(childRun.testToRecordCompletedLabel(), true)
})

test('WHAT[MANAGED-SESSION-012] HFP_fork_pty_blank_command_is_refused', async () => {
  const r = await pty.testBlankCommandRefused()
  assert.equal(r.refused, true)
})

test('WHAT[MANAGED-SESSION-012] HFP_fork_pty_tracks_registers_and_resolves_last', async () => {
  const r = await pty.testTracksAndResolves()
  assert.equal(r.ok, true)
})

test('WHAT[MANAGED-SESSION-012] HFP_fork_pty_port_exception_untracks_and_errors', async () => {
  const r = await pty.testPortExceptionUntracks()
  assert.equal(r.errorPropagated, true)
})

test('WHAT[MANAGED-SESSION-012] HFP_try_pty_unknown_string_id_is_none', async () => {
  const r = await pty.testUnknownIdIsNone()
  assert.equal(r, null)
})

test('WHAT[MANAGED-SESSION-012] HFP_send_pty_unowned_id_is_unknown', async () => {
  const r = await pty.testUnownedIdIsUnknown()
  assert.equal(r.unknown, true)
})

test('WHAT[MANAGED-SESSION-012] HFP_send_pty_owned_but_missing_on_port_is_unknown', async () => {
  const r = await pty.testMissingOnPortIsUnknown()
  assert.equal(r.unknown, true)
})

test('WHAT[MANAGED-SESSION-012] HFP_send_pty_signal_forwards_signal_command', async () => {
  const r = await pty.testSignalForwardsCommand()
  assert.equal(r.forwarded, true)
})

test('WHAT[MANAGED-SESSION-012] HFP_send_pty_write_forwards_write_command', async () => {
  const r = await pty.testWriteForwardsCommand()
  assert.equal(r.forwarded, true)
})

test('WHAT[MANAGED-SESSION-012] HFP_send_pty_read_with_empty_prompt', async () => {
  const r = await pty.testReadEmptyPrompt()
  assert.equal(r.ok, true)
})

test('WHAT[MANAGED-SESSION-012] HFP_send_pty_port_error_propagates', async () => {
  const r = await pty.testPortErrorPropagates()
  assert.equal(r.errorPropagated, true)
})

test('WHAT[MANAGED-SESSION-012] HFP_track_untrack_pty_run_round_trip', async () => {
  const r = await pty.testTrackUntrackRoundTrip()
  assert.equal(r.ok, true)
})
