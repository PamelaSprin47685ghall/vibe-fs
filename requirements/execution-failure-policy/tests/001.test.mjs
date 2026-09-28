import assert from 'node:assert/strict'
import test from 'node:test'
import * as signals from '../../../dist/OpenCode/Host/HostSignalSurface.js'
import * as policy from '../../../dist/Execution/Failure/Surface.js'
import * as provider from '../../../dist/Participant/Provider/Attempt/FailureSurface.js'
import { input } from './support/policy-input.mjs'

const sessionError = (error) => ({ type: 'session.error', properties: { sessionID: 'session-1', error } })

test('WHAT[execution-failure-policy-001] Host structural controls remain distinct from provider errors', () => {
  for (const name of ['TimeoutError', 'ProviderAuthError', 'PermissionDeniedError', 'ProviderError', 'StreamInterruptedError', 'whatever', undefined]) {
    assert.equal(signals.tryDecode(sessionError({ name, message: 'fatal wording' })).failure, 'ProviderTransient', String(name))
  }
  assert.equal(signals.tryDecode(sessionError({ name: 'MessageAbortedError' })).failure, 'UserCancelled')
  assert.equal(signals.tryDecode(sessionError({ name: 'SupersededError' })).failure, 'Superseded')
})

test('WHAT[execution-failure-policy-001] adapter rejects unclassified failure and persistence commitment instead of a default decision', () => {
  for (const failure of ['Timeout', 'Retryable', 'UnknownFailure', { kind: 'PersistenceFailure', commitment: 'Maybe' }]) {
    assert.throws(() => policy.decide(input({ failure })), /unknown (execution failure|persistence commitment)/)
  }
})

test('WHAT[execution-failure-policy-001] provider adapter preserves kind exact identity and first-token evidence', () => {
  assert.deepEqual(provider.classify({
    providerRun: 'run-17', requestKind: 'WorkMain', status: 'Transient', firstTokenObserved: false, diagnostic: 'never retry',
  }), {
    failure: 'ProviderTransient', providerRun: 'run-17', requestKind: 'work-main', firstTokenObserved: false, diagnostic: 'never retry',
  })
  const permanent = provider.classify({
    providerRun: 'run-18', requestKind: 'BloggerSquash', status: 'Permanent', firstTokenObserved: false, diagnostic: 'timeout',
  })
  assert.equal(permanent.failure, 'ProviderPermanent')
  assert.equal(permanent.providerRun, 'run-18')
  assert.equal(permanent.requestKind, 'blogger-squash')
  const interrupted = provider.classify({
    providerRun: 'run-19', requestKind: 'InteractionRepair', status: 'Transient', firstTokenObserved: true, diagnostic: 'retryable',
  })
  assert.equal(interrupted.failure, 'StreamInterruptedAfterFirstToken')
  assert.equal(interrupted.providerRun, 'run-19')
  assert.equal(interrupted.requestKind, 'interaction-repair')
})
