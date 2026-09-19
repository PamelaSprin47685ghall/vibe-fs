import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const child = await import("../../../dist/Execution/Delegation/Fork/ChildRecoverySurface.js");
const handles = await import("../../../dist/Execution/Delegation/Handle/Surface.js");
const join = await import("../../../dist/Execution/Delegation/Fork/OpenCode/JoinSurface.js");


test('WHAT[crash-reconciliation-009] P0_CLEAN_BREAK_delayed_recovery_before_ready_no_aborted_join_then_true_terminal', () => {
  const waiting = child.resolve('active', 'missing', ['aborted:interrupted tool', 'restore'], '')
  assert.equal(waiting.result, 'RecoveryIncomplete')
  assert.equal(handles.crashScenario('active').retired, false)
  const terminal = child.resolve('active', 'terminal', [], 'real work done')
  assert.equal(terminal.result, 'RecoveredTerminal')
  const wire = join.renderBatch('english', [{ kind: 'completed', agentId: 'h1', agentName: 'coder', role: 'Coder', runId: 'run-h1', workRecord: 'real work done' }])
  assert.ok(!wire.includes('status = "aborted"'))
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const handles = await import("../../../dist/Execution/Delegation/Handle/Surface.js");

const active = () => handles.crashScenario('active')

test('WHAT[crash-reconciliation-009] P0_RECOVERY_JOIN_001_crash_after_aborted_observed_stays_active', () => {
  const state = active()
  assert.equal(state.lifecycle, 'Active')
  assert.equal(state.joinable, 0)
  assert.equal(state.retired, false)
})
test('WHAT[crash-reconciliation-009] P0_RECOVERY_JOIN_001_crash_matrix_no_aborted_durable_fact', () => {
  const state = active()
  assert.equal(state.lifecycle, 'Active')
  assert.equal(state.completion, null)
  assert.equal(state.abandonReason, null)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const childRecovery = await import("../../../dist/Execution/Delegation/Fork/ChildRecoverySurface.js");

const CHILD = 'ses_trace_child'
const AGENT = 'coder'
const event = (kind, payload = {}) => ({ kind, ...payload })
const legal = [
  event('RawAbortObserved', { session: CHILD }),
  event('ChildRecoveryStarted', { session: CHILD }),
  event('TerminalProofIssued', { agent: AGENT }),
  event('HandleCompletionCommitted', { agent: AGENT }),
  event('JoinReturned', { agent: AGENT }),
]

test('WHAT[crash-reconciliation-009] P0_RECOVERY_JOIN_001_trace_legal_order_passes', () => {
  assert.equal(childRecovery.trace(legal), true)
})
test('WHAT[crash-reconciliation-009] P0_RECOVERY_JOIN_001_trace_join_without_proof_fails', () => {
  assert.equal(
    childRecovery.trace([
      event('ChildRecoveryStarted', { session: CHILD }),
      event('HandleCompletionCommitted', { agent: AGENT }),
      event('JoinReturned', { agent: AGENT }),
    ]),
    false,
  )
})
test('WHAT[crash-reconciliation-009] P0_RECOVERY_JOIN_001_trace_join_without_commit_fails', () => {
  assert.equal(
    childRecovery.trace([
      event('TerminalProofIssued', { agent: AGENT }),
      event('JoinReturned', { agent: AGENT }),
    ]),
    false,
  )
})
test('WHAT[crash-reconciliation-009] P0_RECOVERY_JOIN_001_trace_proof_after_commit_fails', () => {
  assert.equal(
    childRecovery.trace([
      event('HandleCompletionCommitted', { agent: AGENT }),
      event('TerminalProofIssued', { agent: AGENT }),
      event('JoinReturned', { agent: AGENT }),
    ]),
    false,
  )
})
test('WHAT[crash-reconciliation-009] P0_RECOVERY_JOIN_001_trace_abort_adjacent_commit_fails', () => {
  assert.equal(
    childRecovery.trace([
      event('RawAbortObserved', { session: CHILD }),
      event('HandleCompletionCommitted', { agent: AGENT }),
      event('TerminalProofIssued', { agent: AGENT }),
      event('JoinReturned', { agent: AGENT }),
    ]),
    false,
  )
})
test('WHAT[crash-reconciliation-009] P0_RECOVERY_JOIN_001_trace_abort_adjacent_join_returned_fails', () => {
  assert.equal(
    childRecovery.trace([
      event('TerminalProofIssued', { agent: AGENT }),
      event('HandleCompletionCommitted', { agent: AGENT }),
      event('RawAbortObserved', { session: CHILD }),
      event('JoinReturned', { agent: AGENT }),
    ]),
    false,
  )
})
test('WHAT[crash-reconciliation-009] P0_RECOVERY_JOIN_001_trace_empty_and_abort_only_pass', () => {
  assert.equal(childRecovery.trace([]), true)
  assert.equal(childRecovery.trace([event('RawAbortObserved', { session: CHILD })]), true)
})
test('WHAT[crash-reconciliation-009] P0_RECOVERY_JOIN_001_trace_wrong_agent_proof_does_not_satisfy_join', () => {
  assert.equal(
    childRecovery.trace([
      event('TerminalProofIssued', { agent: 'other-agent' }),
      event('HandleCompletionCommitted', { agent: AGENT }),
      event('JoinReturned', { agent: AGENT }),
    ]),
    false,
  )
})
}
