import assert from 'node:assert/strict'
import test from 'node:test'
import * as policy from '../../../dist/Execution/Failure/Surface.js'

const executionKey = {
  sessionId: 'ses-execfail-013',
  physicalUserMessageId: 'msg-execfail-013',
}
const capacityFence = { reference: 'fence-execfail-013' }
const provider = {
  logicalRun: 'logical-execfail-013',
  providerRun: 'provider-execfail-013',
  requestKind: 'WorkMain',
  retryBudget: 'Available',
  breaker: 'Closed',
}
const baseInput = {
  failure: 'ProtocolRejection',
  phase: 'ProviderStarted',
  executionKey,
  capacityFence,
  provider,
}
const decide = (change = {}) => policy.decide({ ...baseInput, ...change })

test('WHAT[EXECFAIL-013] fatal branches preserve distinct lifecycle semantics and reject coalescing across commitments', () => {
  // 1. AcceptedBeforeProvider vs ProviderStarted distinct terminal resolutions
  const preProviderDecision = decide({
    phase: 'AcceptedBeforeProvider',
    failure: 'LocalInvariant',
  })
  assert.equal(preProviderDecision.resolution, 'TerminalizeAcceptedPreProvider')

  const startedDecision = decide({
    phase: 'ProviderStarted',
    failure: 'LocalInvariant',
  })
  assert.equal(startedDecision.resolution, 'TerminalizeProviderStarted')

  // 2. AcceptanceUnknown must await reconciliation, never coalesced into premature terminal
  const unknownDecision = decide({
    failure: 'AcceptanceUnknown',
  })
  assert.equal(unknownDecision.resolution, 'AwaitAcceptanceReconciliation')

  // 3. NotCommitted persistence failure preserves current fact, stopping before rejected step
  const notCommittedDecision = decide({
    failure: { kind: 'PersistenceFailure', commitment: 'NotCommitted' },
  })
  assert.equal(notCommittedDecision.resolution, 'PreserveCurrentFact')

  // 4. NoAcceptedFact phase preserves fact and does not forge terminal
  const noAcceptedDecision = decide({
    phase: 'NoAcceptedFact',
    failure: 'LocalInvariant',
  })
  assert.equal(noAcceptedDecision.resolution, 'PreserveCurrentFact')
})
