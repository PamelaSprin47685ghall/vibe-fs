import assert from 'node:assert/strict'
import test from 'node:test'

import * as signals from '../../../dist/OpenCode/Host/HostSignalSurface.js'
import * as policy from '../../../dist/Execution/Failure/Surface.js'
import * as provider from '../../../dist/Participant/Provider/Attempt/FailureSurface.js'

const sessionError = (error) => ({
  type: 'session.error',
  properties: { sessionID: 'session-1', error },
})

const executionKey = {
  sessionId: 'ses-failure-policy',
  physicalUserMessageId: 'msg-failure-policy',
}

const capacityFence = { reference: 'fence-failure-policy' }

const providerFacts = {
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
  provider: providerFacts,
}

const failures = [
  'LocalInvariant',
  'ProtocolRejection',
  'AuthorizationDenied',
  'UserCancelled',
  'Superseded',
  'CapacityQueueFull',
  'ProviderTransient',
  'ProviderPermanent',
  'AcceptanceUnknown',
  'StreamInterruptedAfterFirstToken',
  { kind: 'PersistenceFailure', commitment: 'NotCommitted' },
  { kind: 'PersistenceFailure', commitment: 'Committed' },
  { kind: 'PersistenceFailure', commitment: 'Unknown' },
]

test('WHAT[EXECFAIL-001] Host adapter returns closed typed failures from structural evidence', () => {
  assert.equal(signals.tryDecode(sessionError({ name: 'TimeoutError', message: 'fatal wording' })).failure, 'ProviderTransient')
  for (const name of ['ProviderAuthError', 'PermissionDeniedError', 'ProviderError', 'StreamInterruptedError', 'whatever', undefined]) {
    assert.equal(signals.tryDecode(sessionError({ name })).failure, 'ProviderTransient', String(name))
  }
  assert.equal(signals.tryDecode(sessionError({ name: 'MessageAbortedError' })).failure, 'UserCancelled')
  assert.equal(signals.tryDecode(sessionError({ name: 'SupersededError' })).failure, 'Superseded')
})

test('WHAT[EXECFAIL-001] observes every closed failure and persistence commitment variant', () => {
  assert.equal(failures.length, 13)

  for (const failure of failures) {
    const decision = policy.decide({ ...baseInput, failure })
    assert.equal(typeof decision, 'object')
    assert.equal(typeof decision.resolution, 'string')
    assert.ok('breaker' in decision)
    assert.ok('capacitySettlement' in decision)
    assert.ok('fatality' in decision)
  }
})

test('WHAT[EXECFAIL-001] adapter returns typed ProviderTransient', () => {
  assert.deepEqual(provider.classify({
    providerRun: 'run-17', requestKind: 'WorkMain', status: 'Transient', firstTokenObserved: false, diagnostic: 'never retry',
  }), {
    failure: 'ProviderTransient', providerRun: 'run-17', requestKind: 'work-main', firstTokenObserved: false, diagnostic: 'never retry',
  })
})

test('WHAT[EXECFAIL-001] provider adapter preserves permanent kind and exact attempt identity', () => {
  const result = provider.classify({
    providerRun: 'run-18', requestKind: 'BloggerSquash', status: 'Permanent', firstTokenObserved: false, diagnostic: 'timeout',
  })
  assert.equal(result.failure, 'ProviderPermanent')
  assert.equal(result.providerRun, 'run-18')
  assert.equal(result.requestKind, 'blogger-squash')
})

test('WHAT[EXECFAIL-001] first-token evidence maps interruption without transparent retry classification', () => {
  const result = provider.classify({
    providerRun: 'run-19', requestKind: 'InteractionRepair', status: 'Transient', firstTokenObserved: true, diagnostic: 'retryable',
  })
  assert.equal(result.failure, 'StreamInterruptedAfterFirstToken')
  assert.equal(result.providerRun, 'run-19')
  assert.equal(result.requestKind, 'interaction-repair')
})
