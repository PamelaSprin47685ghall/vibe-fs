import assert from 'node:assert/strict'
import test from 'node:test'

import * as provider from '../../../dist/Participant/Provider/Attempt/FailureSurface.js'

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

test('WHAT[EXECFAIL-008] provider diagnostic text never drives classification', () => {
  for (const diagnostic of ['auth failure', 'rate limited', 'permanent fatal']) {
    assert.equal(provider.classify({
      providerRun: 'run-20', requestKind: 'StrengthReplica', status: 'Transient', firstTokenObserved: false, diagnostic,
    }).failure, 'ProviderTransient')
  }
})
