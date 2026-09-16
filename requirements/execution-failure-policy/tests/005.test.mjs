import assert from 'node:assert/strict'
import test from 'node:test'

import * as policy from '../../../dist/Execution/Failure/Surface.js'

const executionKey = {
  sessionId: 'ses-failure-policy',
  physicalUserMessageId: 'msg-failure-policy',
}

const capacityFence = { reference: 'fence-failure-policy' }

const provider = {
  logicalRun: 'logical-failure-policy',
  providerRun: 'provider-failure-policy',
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

test('WHAT[EXECFAIL-005] terminal resolution carries the exact execution key and typed disposition', () => {
  const expected = [
    ['NoAcceptedFact', 'PreserveCurrentFact'],
    ['AcceptedBeforeProvider', 'TerminalizeAcceptedPreProvider'],
    ['ProviderStarted', 'TerminalizeProviderStarted'],
    ['Terminal', 'PreserveCurrentFact'],
  ]

  for (const [phase, expectedKind] of expected) {
    const decision = decide({ phase, failure: 'AuthorizationDenied' })
    assert.equal(decision.resolution, expectedKind)
    if (expectedKind.startsWith('Terminalize')) {
      assert.deepEqual(decision.executionKey, executionKey)
      assert.equal(decision.terminalDisposition, 'Rejected')
    }
  }

  assert.equal(decide({ failure: 'UserCancelled' }).resolution, 'TerminalizeProviderStarted')
  assert.equal(decide({ failure: 'UserCancelled' }).terminalDisposition, 'Cancelled')
  assert.equal(decide({ failure: 'Superseded' }).terminalDisposition, 'Cancelled')
  assert.equal(
    decide({ failure: 'StreamInterruptedAfterFirstToken' }).terminalDisposition,
    'Failed',
  )
})
