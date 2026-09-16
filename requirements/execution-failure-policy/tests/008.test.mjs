import assert from 'node:assert/strict'
import test from 'node:test'

import * as signals from '../../../dist/OpenCode/Host/HostSignalSurface.js'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'
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

const decide = (change = {}) => policy.decide({ ...baseInput, ...change })

test('WHAT[EXECFAIL-008] Host classification ignores diagnostic wording', () => {
  const transient = signals.tryDecode(sessionError({ name: 'TimeoutError', message: 'permission denied forever' }))
  const rewritten = signals.tryDecode(sessionError({ name: 'TimeoutError', message: 'please retry' }))
  assert.equal(transient.failure, 'ProviderTransient')
  assert.equal(rewritten.failure, transient.failure)
  assert.notEqual(rewritten.diagnostic, transient.diagnostic)
})

test('WHAT[EXECFAIL-008] persistence diagnostics cannot change commitment', () => {
  for (const diagnostic of ['definitely succeeded', 'definitely failed', 'retry me']) {
    assert.equal(journal.JournalSurface_mapAppendFailure({ kind: 'WriteUnknown', diagnostic }).commitment, 'Unknown')
  }
})

test('WHAT[EXECFAIL-008] policy is deterministic and ignores diagnostic or temporal decoration', () => {
  const typed = decide({ failure: 'ProviderTransient' })
  const decorated = decide({
    failure: 'ProviderTransient',
    diagnostic: 'timeout, unauthorized, cancelled',
    elapsedMilliseconds: Number.MAX_SAFE_INTEGER,
    retryCount: Number.MAX_SAFE_INTEGER,
  })

  assert.deepEqual(decorated, typed)
  assert.deepEqual(decide({ failure: 'ProviderTransient' }), typed)
})

test('WHAT[EXECFAIL-008] provider diagnostic text never drives classification', () => {
  for (const diagnostic of ['auth failure', 'rate limited', 'permanent fatal']) {
    assert.equal(provider.classify({
      providerRun: 'run-20', requestKind: 'StrengthReplica', status: 'Transient', firstTokenObserved: false, diagnostic,
    }).failure, 'ProviderTransient')
  }
})
