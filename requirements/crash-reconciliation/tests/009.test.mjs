import assert from 'node:assert/strict'
import test from 'node:test'
import * as cleanBreak from '../../../dist/Execution/Delegation/Fork/JoinCleanBreakRecoverySurface.js'
import * as crashMatrix from '../../../dist/Execution/Delegation/Fork/JoinRecoveryCrashMatrixSurface.js'
import * as trace from '../../../dist/Execution/Delegation/Fork/JoinRecoveryTraceSurface.js'

test('WHAT[CRASH-009] P0_CLEAN_BREAK_delayed_recovery_before_ready_no_aborted_join_then_true_terminal', () => {
  const res = cleanBreak.scenario('delayed-recovery')
  assert.equal(res.abortedJoin, false)
})

test('WHAT[CRASH-009] P0_RECOVERY_JOIN_001_crash_after_aborted_observed_stays_active', () => {
  const res = crashMatrix.scenario('aborted-observed')
  assert.equal(res.state, 'Active')
})

test('WHAT[CRASH-009] P0_RECOVERY_JOIN_001_crash_matrix_no_aborted_durable_fact', () => {
  const res = crashMatrix.scenario('no-aborted-fact')
  assert.equal(res.hasAbortedFact, false)
})

test('WHAT[CRASH-009] P0_RECOVERY_JOIN_001_trace_legal_order_passes', () => {
  assert.equal(trace.verify(['proof', 'commit', 'join']), true)
})

test('WHAT[CRASH-009] P0_RECOVERY_JOIN_001_trace_join_without_proof_fails', () => {
  assert.equal(trace.verify(['commit', 'join']), false)
})

test('WHAT[CRASH-009] P0_RECOVERY_JOIN_001_trace_join_without_commit_fails', () => {
  assert.equal(trace.verify(['proof', 'join']), false)
})

test('WHAT[CRASH-009] P0_RECOVERY_JOIN_001_trace_proof_after_commit_fails', () => {
  assert.equal(trace.verify(['commit', 'proof', 'join']), false)
})

test('WHAT[CRASH-009] P0_RECOVERY_JOIN_001_trace_abort_adjacent_commit_fails', () => {
  assert.equal(trace.verify(['abort', 'commit']), false)
})

test('WHAT[CRASH-009] P0_RECOVERY_JOIN_001_trace_abort_adjacent_join_returned_fails', () => {
  assert.equal(trace.verify(['abort', 'join']), false)
})

test('WHAT[CRASH-009] P0_RECOVERY_JOIN_001_trace_empty_and_abort_only_pass', () => {
  assert.equal(trace.verify([]), true)
  assert.equal(trace.verify(['abort']), true)
})

test('WHAT[CRASH-009] P0_RECOVERY_JOIN_001_trace_wrong_agent_proof_does_not_satisfy_join', () => {
  assert.equal(trace.verifyWithAgent('agent-1', 'agent-2'), false)
})
