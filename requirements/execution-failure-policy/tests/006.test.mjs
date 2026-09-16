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

test('WHAT[EXECFAIL-006] LocalInvariant requests fatality only after typed settlement commands', () => {
  const decision = decide({ failure: 'LocalInvariant', phase: 'AcceptedBeforeProvider' })
  assert.equal(decision.resolution, 'TerminalizeAcceptedPreProvider')
  assert.equal(decision.breaker.kind, 'NoBreakerTransition')
  assert.deepEqual(decision.capacitySettlement, {
    kind: 'ReleaseExactFence',
    fenceReference: capacityFence.reference,
  })
  assert.equal(decision.fatality.kind, 'FatalAfterSettlement')
})
