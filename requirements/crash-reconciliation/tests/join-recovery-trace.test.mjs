/**
 * P0-RECOVERY-JOIN-001 §九: JoinRecoveryTrace pure invariant.
 */
import assert from 'node:assert/strict'
import test from 'node:test'
import { childRecovery, sessionId } from '../support/domain.mjs'

const CHILD = sessionId('ses_trace_child')
const AGENT = 'fast-coder'

test('P0_RECOVERY_JOIN_001_trace_legal_order_passes', () => {
  const events = [
    childRecovery.rawAbortObserved(CHILD),
    childRecovery.childRecoveryStarted(CHILD),
    childRecovery.terminalProofIssued(AGENT),
    childRecovery.handleCompletionCommitted(AGENT),
    childRecovery.joinReturned(AGENT, childRecovery.finalitySucceeded('{"ok":true}')),
  ]
  assert.equal(childRecovery.joinReturnedImpliesProofBeforeCommit(events), true)
})

test('P0_RECOVERY_JOIN_001_trace_join_without_proof_fails', () => {
  const events = [
    childRecovery.childRecoveryStarted(CHILD),
    childRecovery.handleCompletionCommitted(AGENT),
    childRecovery.joinReturned(AGENT, childRecovery.finalitySucceeded('body')),
  ]
  assert.equal(childRecovery.joinReturnedImpliesProofBeforeCommit(events), false)
})

test('P0_RECOVERY_JOIN_001_trace_join_without_commit_fails', () => {
  const events = [
    childRecovery.terminalProofIssued(AGENT),
    childRecovery.joinReturned(AGENT, childRecovery.finalitySucceeded('body')),
  ]
  assert.equal(childRecovery.joinReturnedImpliesProofBeforeCommit(events), false)
})

test('P0_RECOVERY_JOIN_001_trace_proof_after_commit_fails', () => {
  const events = [
    childRecovery.handleCompletionCommitted(AGENT),
    childRecovery.terminalProofIssued(AGENT),
    childRecovery.joinReturned(AGENT, childRecovery.finalitySucceeded('body')),
  ]
  assert.equal(childRecovery.joinReturnedImpliesProofBeforeCommit(events), false)
})

test('P0_RECOVERY_JOIN_001_trace_abort_adjacent_commit_fails', () => {
  const events = [
    childRecovery.rawAbortObserved(CHILD),
    childRecovery.handleCompletionCommitted(AGENT),
    childRecovery.terminalProofIssued(AGENT),
    childRecovery.joinReturned(AGENT, childRecovery.finalitySucceeded('body')),
  ]
  assert.equal(childRecovery.joinReturnedImpliesProofBeforeCommit(events), false)
})

test('P0_RECOVERY_JOIN_001_trace_abort_adjacent_join_returned_fails', () => {
  const events = [
    childRecovery.terminalProofIssued(AGENT),
    childRecovery.handleCompletionCommitted(AGENT),
    childRecovery.rawAbortObserved(CHILD),
    childRecovery.joinReturned(AGENT, childRecovery.finalitySucceeded('body')),
  ]
  assert.equal(childRecovery.joinReturnedImpliesProofBeforeCommit(events), false)
})

test('P0_RECOVERY_JOIN_001_trace_empty_and_abort_only_pass', () => {
  assert.equal(childRecovery.joinReturnedImpliesProofBeforeCommit([]), true)
  assert.equal(
    childRecovery.joinReturnedImpliesProofBeforeCommit([childRecovery.rawAbortObserved(CHILD)]),
    true,
  )
})

test('P0_RECOVERY_JOIN_001_trace_wrong_agent_proof_does_not_satisfy_join', () => {
  const events = [
    childRecovery.terminalProofIssued('other-agent'),
    childRecovery.handleCompletionCommitted(AGENT),
    childRecovery.joinReturned(AGENT, childRecovery.finalitySucceeded('body')),
  ]
  assert.equal(childRecovery.joinReturnedImpliesProofBeforeCommit(events), false)
})
