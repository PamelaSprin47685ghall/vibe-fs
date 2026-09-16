// P0-RECOVERY-JOIN-001 §九: JoinRecoveryTrace pure invariant through the
// crash-owned ChildRecovery surface. Trace events are plain observations; the
// typed trace and finality remain behind the owner boundary.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as childRecovery from '../../../dist/Execution/Delegation/Fork/ChildRecoverySurface.js'

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

test('WHAT[CRASH-009] P0_RECOVERY_JOIN_001_trace_legal_order_passes', () => {
  assert.equal(childRecovery.trace(legal), true)
})

test('WHAT[CRASH-009] P0_RECOVERY_JOIN_001_trace_join_without_proof_fails', () => {
  assert.equal(
    childRecovery.trace([
      event('ChildRecoveryStarted', { session: CHILD }),
      event('HandleCompletionCommitted', { agent: AGENT }),
      event('JoinReturned', { agent: AGENT }),
    ]),
    false,
  )
})

test('WHAT[CRASH-009] P0_RECOVERY_JOIN_001_trace_join_without_commit_fails', () => {
  assert.equal(
    childRecovery.trace([
      event('TerminalProofIssued', { agent: AGENT }),
      event('JoinReturned', { agent: AGENT }),
    ]),
    false,
  )
})

test('WHAT[CRASH-009] P0_RECOVERY_JOIN_001_trace_proof_after_commit_fails', () => {
  assert.equal(
    childRecovery.trace([
      event('HandleCompletionCommitted', { agent: AGENT }),
      event('TerminalProofIssued', { agent: AGENT }),
      event('JoinReturned', { agent: AGENT }),
    ]),
    false,
  )
})

test('WHAT[CRASH-009] P0_RECOVERY_JOIN_001_trace_abort_adjacent_commit_fails', () => {
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

test('WHAT[CRASH-009] P0_RECOVERY_JOIN_001_trace_abort_adjacent_join_returned_fails', () => {
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

test('WHAT[CRASH-009] P0_RECOVERY_JOIN_001_trace_empty_and_abort_only_pass', () => {
  assert.equal(childRecovery.trace([]), true)
  assert.equal(childRecovery.trace([event('RawAbortObserved', { session: CHILD })]), true)
})

test('WHAT[CRASH-009] P0_RECOVERY_JOIN_001_trace_wrong_agent_proof_does_not_satisfy_join', () => {
  assert.equal(
    childRecovery.trace([
      event('TerminalProofIssued', { agent: 'other-agent' }),
      event('HandleCompletionCommitted', { agent: AGENT }),
      event('JoinReturned', { agent: AGENT }),
    ]),
    false,
  )
})
