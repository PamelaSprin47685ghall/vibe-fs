// tests/unit/session/child-run-projection.test.mjs — VERIFY-009 coverage target.
//
// ChildRun lifecycle (completion cell, cancellation) and the pure projection of
// a run's physical state into AgentRecord status/label fields.

import assert from 'node:assert/strict'
import test from 'node:test'

import { caseOf } from '../../verification-system/tests/support/domain.mjs'

const {
  ChildRunModule_bindSession,
  ChildRunModule_cancel,
  ChildRunModule_create,
  ChildRunModule_isActive,
  ChildRunModule_isCancelled,
  ChildRunModule_isCompleted,
  ChildRunModule_makeCompleted,
  ChildRunModule_makeFailed,
  ChildRunModule_tryComplete,
} = await import('../../../dist/Execution/Delegation/Fork/ChildRun.js')

const { status, toRecord } = await import('../../../dist/Execution/Delegation/Fork/ChildRunProjection.js')

const {
  AgentCompletion_abandoned,
  AgentCompletion_failed,
  AgentCompletion_ofSimpleText,
} = await import('../../../dist/Execution/Session/AgentCompletion.js')

const { Role } = await import('../../../dist/Foundation/Roles.js')
const { SessionId } = await import('../../../dist/Foundation/Identity.js')

const makeRun = () => ChildRunModule_create('agent-1', 'run-1', 'fast-coder', Role.Manager, 'do the thing')
const complete = (run, outcome) => {
  const completion = ChildRunModule_makeCompleted(run, outcome)
  assert.equal(ChildRunModule_tryComplete(run, completion), true)
  return completion
}

// ── ChildRun lifecycle ───────────────────────────────────────────────────────

test('WHAT[MANAGED-SESSION-012] VERIFY_009_child_run_starts_active', () => {
  const run = makeRun()
  assert.equal(ChildRunModule_isActive(run), true)
  assert.equal(ChildRunModule_isCompleted(run), false)
  assert.equal(ChildRunModule_isCancelled(run), false)
})

test('WHAT[MANAGED-SESSION-012] VERIFY_009_child_run_cancel_flips_active_and_cancelled', () => {
  const run = makeRun()
  ChildRunModule_cancel(run)
  assert.equal(ChildRunModule_isCancelled(run), true)
  assert.equal(ChildRunModule_isActive(run), false)
  assert.equal(ChildRunModule_isCompleted(run), false)
})

test('WHAT[MANAGED-SESSION-012] VERIFY_009_child_run_bind_session_records_child_session', () => {
  const run = makeRun()
  assert.equal(run.ChildSessionId, undefined)
  ChildRunModule_bindSession(run, new SessionId('ses-child-9'))
  assert.equal(caseOf(run.ChildSessionId), 'SessionId')
  assert.equal(run.ChildSessionId.fields[0], 'ses-child-9')
})

test('WHAT[MANAGED-SESSION-012] VERIFY_009_child_run_completion_cell_is_single_assignment', () => {
  const run = makeRun()
  const first = complete(run, AgentCompletion_ofSimpleText('agent-1', 'run-1', Role.Manager, 'done'))
  assert.equal(ChildRunModule_tryComplete(run, first), false, 'second write must be refused')
  assert.equal(ChildRunModule_isCompleted(run), true)
  assert.equal(ChildRunModule_isActive(run), false)
})

test('WHAT[MANAGED-SESSION-012] VERIFY_009_child_run_make_failed_carries_error_outcome', () => {
  const run = makeRun()
  const completion = ChildRunModule_makeFailed(run, 'boom')
  assert.equal(ChildRunModule_tryComplete(run, completion), true)
  assert.equal(caseOf(completion.Outcome), 'AgentFailed')
  assert.equal(completion.Outcome.fields[0].Message, 'boom')
})

// ── status ───────────────────────────────────────────────────────────────────

test('WHAT[MANAGED-SESSION-012] VERIFY_009_projection_status_busy_while_running', () => {
  const run = makeRun()
  assert.equal(caseOf(status(false, run)), 'Busy')
})

test('WHAT[MANAGED-SESSION-012] VERIFY_009_projection_status_closed_on_cancel_or_runtime_cancel', () => {
  const run = makeRun()
  ChildRunModule_cancel(run)
  assert.equal(caseOf(status(false, run)), 'Closed')
  assert.equal(caseOf(status(true, makeRun())), 'Closed')
})

test('WHAT[MANAGED-SESSION-012] VERIFY_009_projection_status_interrupted_on_interrupt_code', () => {
  const run = makeRun()
  complete(run, AgentCompletion_failed('agent-1', 'run-1', Role.Manager, undefined, 'INTERRUPTED', 'interrupted by user'))
  assert.equal(caseOf(status(false, run)), 'Interrupted')
})

test('WHAT[MANAGED-SESSION-012] VERIFY_009_projection_status_closed_on_abandon', () => {
  const run = makeRun()
  complete(run, AgentCompletion_abandoned('agent-1', 'gave up'))
  assert.equal(caseOf(status(false, run)), 'Closed')
})

test('WHAT[MANAGED-SESSION-012] VERIFY_009_projection_status_idle_on_clean_completion', () => {
  const run = makeRun()
  complete(run, AgentCompletion_ofSimpleText('agent-1', 'run-1', Role.Manager, 'done'))
  assert.equal(caseOf(status(false, run)), 'Idle')
})

test('WHAT[MANAGED-SESSION-012] VERIFY_009_projection_status_idle_for_other_failures', () => {
  const run = makeRun()
  complete(run, AgentCompletion_failed('agent-1', 'run-1', Role.Manager, undefined, 'TIMEOUT', 'too slow'))
  assert.equal(caseOf(status(false, run)), 'Idle')
})

// ── toRecord ─────────────────────────────────────────────────────────────────

test('WHAT[MANAGED-SESSION-012] VERIFY_009_projection_to_record_running_state', () => {
  const run = makeRun()
  const record = toRecord(false, 'agent-1', run)
  assert.equal(record.AgentId, 'agent-1')
  assert.equal(record.Agent, 'fast-coder')
  assert.equal(caseOf(record.Role), 'Manager')
  assert.equal(caseOf(record.Status), 'Busy')
  assert.equal(record.CurrentRunId, 'run-1')
  assert.equal(record.TerminalStatusLabel, undefined)
  assert.equal(record.CompletionCellSettled, false)
  assert.equal(record.ChildSessionId, undefined)
})

test('WHAT[MANAGED-SESSION-012] VERIFY_009_projection_to_record_interrupted_label_is_message', () => {
  const run = makeRun()
  complete(run, AgentCompletion_failed('agent-1', 'run-1', Role.Manager, undefined, 'INTERRUPTED', 'stop now'))
  const record = toRecord(false, 'agent-1', run)
  assert.equal(record.TerminalStatusLabel, 'stop now')
  assert.equal(record.CurrentRunId, undefined)
  assert.equal(record.CompletionCellSettled, true)
  assert.equal(caseOf(record.Status), 'Interrupted')
})

test('WHAT[MANAGED-SESSION-012] VERIFY_009_projection_to_record_abandoned_label_is_reason', () => {
  const run = makeRun()
  complete(run, AgentCompletion_abandoned('agent-1', 'no longer joinable'))
  const record = toRecord(false, 'agent-1', run)
  assert.equal(record.TerminalStatusLabel, 'no longer joinable')
  assert.equal(caseOf(record.Status), 'Closed')
})

test('WHAT[MANAGED-SESSION-012] VERIFY_009_projection_to_record_completed_label_is_status_text', () => {
  const run = makeRun()
  complete(run, AgentCompletion_ofSimpleText('agent-1', 'run-1', Role.Manager, 'done'))
  const record = toRecord(false, 'agent-1', run)
  assert.equal(record.TerminalStatusLabel, 'completed')
  assert.equal(caseOf(record.Status), 'Idle')
})
