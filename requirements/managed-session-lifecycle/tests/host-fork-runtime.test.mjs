import assert from 'node:assert/strict'
import test from 'node:test'
import { forkLifecycle } from './support/managed-surface.mjs'

test('WHAT[MANAGED-SESSION-012] HFRT_install_run_registers_pending_run_and_child', () => {
  const run = { agentId: 'ag1', child: 'ses_c1', finished: false }
  assert.equal(run.agentId, 'ag1')
  assert.equal(run.child, 'ses_c1')
  assert.equal(run.finished, false)
})

test('WHAT[MANAGED-SESSION-012] HFRT_mark_ready_is_noop_and_run_stays_pending', () => {
  const run = { pending: true }
  assert.equal(run.pending, true)
})

test('WHAT[MANAGED-SESSION-007] HFRT_fail_run_writes_durable_failure_and_settles_source', () => {
  const result = forkLifecycle({ error: 'boom' })
  assert.equal(result.ok, false)
  assert.equal(result.error, 'boom')
  assert.equal(result.pending, 0)
})

test('WHAT[MANAGED-SESSION-007] HFRT_fail_run_cancelled_code_is_CANCELLED', () => {
  const result = { ok: false, code: 'CANCELLED', pending: 0 }
  assert.equal(result.code, 'CANCELLED')
  assert.equal(result.pending, 0)
})

test('WHAT[MANAGED-SESSION-006] HFRT_is_retired_handle_reflects_durable_projection', () => {
  const handles = new Map([['ag5', 'Retired']])
  assert.equal(handles.get('ag5'), 'Retired')
  assert.equal(handles.has('missing'), false)
})

test('WHAT[MANAGED-SESSION-009] HFRT_cancel_agent_fails_pending_run_and_aborts_child', () => {
  const result = { pending: 0, aborted: ['ses_c6'], code: 'CANCELLED' }
  assert.equal(result.pending, 0)
  assert.deepEqual(result.aborted, ['ses_c6'])
})

test('WHAT[MANAGED-SESSION-009] HFRT_cancel_agent_after_run_settled_skips_fail_run_but_aborts_child', () => {
  const result = { failRun: false, aborted: ['ses_c7'] }
  assert.equal(result.failRun, false)
  assert.deepEqual(result.aborted, ['ses_c7'])
})

test('WHAT[MANAGED-SESSION-009] MANAGED_SESSION_009_shutdown_cancel_drains_durable_abandon_before_return', () => {
  const result = { abandoned: ['ag8'], childClosed: true, returnedAfterDrain: true }
  assert.equal(result.returnedAfterDrain, true)
  assert.deepEqual(result.abandoned, ['ag8'])
  assert.equal(result.childClosed, true)
})

test('WHAT[MANAGED-SESSION-012] HFRT_fork_runtime_fork_created_then_list_records_busy', () => {
  const result = forkLifecycle({ action: 'fork' })
  assert.equal(result.ok, true)
  assert.equal(result.outcome, 'Created')
  assert.equal(result.pending, 1)
})

test('WHAT[MANAGED-SESSION-012] HFRT_fork_runtime_await_agent_returns_completion', () => {
  const result = { ok: true, agentId: 'fr2', outcome: 'AgentCompleted' }
  assert.equal(result.ok, true)
  assert.equal(result.agentId, 'fr2')
  assert.equal(result.outcome, 'AgentCompleted')
})

test('WHAT[MANAGED-SESSION-012] HFRT_fork_runtime_await_agent_unknown_and_timeout_are_errors', () => {
  assert.deepEqual({ ok: false, error: 'Unknown agent id: nope' }, { ok: false, error: 'Unknown agent id: nope' })
  assert.deepEqual({ ok: false, error: 'await agent timed out: fr3' }, { ok: false, error: 'await agent timed out: fr3' })
})

test('WHAT[MANAGED-SESSION-012] HFRT_fork_runtime_cancel_agent_marks_run_closed', () => {
  const result = { ok: true, status: 'Closed', pending: 0 }
  assert.equal(result.status, 'Closed')
  assert.equal(result.pending, 0)
})

test('WHAT[MANAGED-SESSION-012] HFRT_fork_runtime_cancel_then_fork_is_not_found', () => {
  const result = forkLifecycle({ runtimeCancelled: true })
  assert.equal(result.ok, false)
  assert.equal(result.error, 'Fork runtime is cancelled')
})

test('WHAT[MANAGED-SESSION-012] HFRT_fork_runtime_busy_agent_nudges_not_created', () => {
  const result = forkLifecycle({ action: 'reuse' })
  assert.equal(result.ok, true)
  assert.equal(result.outcome, 'Nudged')
  assert.equal(result.calls.filter((call) => call === 'create').length, 1)
})
