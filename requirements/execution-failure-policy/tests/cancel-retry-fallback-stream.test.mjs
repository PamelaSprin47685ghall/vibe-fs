import assert from 'node:assert/strict'
import test from 'node:test'

import * as policy from '../../../dist/Execution/Failure/Surface.js'
import * as recovery from '../../../dist/Execution/Session/ChatExecution/RecoveryRuntimeSurface.js'
import * as hostSignals from '../../../dist/OpenCode/Host/HostSignalSurface.js'
import * as hooks from '../../../dist/OpenCode/Host/PluginHooksSurface.js'
import * as transaction from '../../../dist/OpenCode/Host/ChatAdmission/TransactionSurface.js'

const executionKey = {
  sessionId: 'ses-cancel-retry-fallback-stream',
  physicalUserMessageId: 'msg-cancel-retry-fallback-stream',
}
const capacityFence = { reference: 'fence-cancel-retry-fallback-stream' }
const provider = {
  logicalRun: 'logical-cancel-retry-fallback-stream',
  providerRun: 'provider-cancel-retry-fallback-stream',
  requestKind: 'WorkMain',
  retryBudget: 'Available',
  fallbackBudget: 'Available',
  breaker: 'Closed',
}
const input = (failure, change = {}) => ({
  failure,
  phase: 'ProviderStarted',
  executionKey,
  capacityFence,
  provider,
  ...change,
})
const decide = (failure, change) => policy.decide(input(failure, change))
const ownerActions = (decision) => [decision.retry, decision.fallback]
  .filter((action) => action.kind !== 'NoRetry' && action.kind !== 'NoFallback')

const terminal = ({ finish, error, providerRun = provider.providerRun } = {}) => ({
  type: 'message.updated',
  properties: {
    info: {
      sessionID: executionKey.sessionId,
      id: providerRun,
      role: 'assistant',
      parentID: executionKey.physicalUserMessageId,
      time: { created: 1, completed: 2 },
      ...(finish === undefined ? {} : { finish }),
      ...(error === undefined ? {} : { error }),
    },
  },
})

const transactionEvidence = (suffix) => ({
  sessionId: `ses-policy-${suffix}`,
  physicalUserMessageId: `msg-policy-${suffix}`,
  logicalRunId: `logical-policy-${suffix}`,
  authorityRootUserMessageId: `root-policy-${suffix}`,
  providerRun: `provider-policy-${suffix}`,
  identitySeed: { participantIdentity: { selectedAgent: 'fast-coder' } },
})

const assertNoRecovery = (decision) => {
  assert.equal(decision.retry.kind, 'NoRetry')
  assert.equal(decision.fallback.kind, 'NoFallback')
  assert.equal(ownerActions(decision).length, 0)
}

const assertSingleRecovery = (decision, expected) => {
  assert.equal(ownerActions(decision).length, 1)
  assert.equal(ownerActions(decision)[0].kind, expected)
}

test('WHAT[EXECFAIL-002] cancel/retry/fallback/stream matrix is interpreted by registered owners', async () => {
  const cancelled = decide('UserCancelled')
  assertNoRecovery(cancelled)
  assert.equal(cancelled.messageDisposition.disposition, 'Cancelled')
  assert.deepEqual(cancelled.capacitySettlement, {
    kind: 'ReleaseExactFence',
    fenceReference: capacityFence.reference,
  })
  assert.equal(cancelled.fatality.kind, 'NoFatality')
  assert.deepEqual(hostSignals.tryDecodeExactProviderTerminal(terminal({ error: { name: 'AbortError' } })), {
    sessionId: executionKey.sessionId,
    physicalUserMessageId: executionKey.physicalUserMessageId,
    providerRun: provider.providerRun,
    outcome: 'Cancelled',
    failure: 'UserCancelled',
    disposition: 'Cancelled',
  })
  const cancelledPromise = transaction.transactionScenario(
    transactionEvidence('cancelled'),
    'AcquireCancelled',
    'None',
  )
  assert.equal(typeof cancelledPromise.then, 'function')
  const cancelledAdmission = await cancelledPromise
  assert.equal(cancelledAdmission.outcome, 'Cancelled')
  assert.equal(cancelledAdmission.providerCount, 0)
  assert.equal(cancelledAdmission.admission.activeCapacity, 0)
  assert.equal(hooks.hookFailurePolicy('UserCancelled', 'ExactSettlementComplete'), 'RethrowUnchanged')

  const transient = decide('ProviderTransient')
  assertSingleRecovery(transient, 'RetryFreshAttempt')
  assert.equal(transient.messageDisposition.kind, 'KeepCurrentFact')
  assert.equal(transient.fatality.kind, 'NoFatality')
  assert.deepEqual(hostSignals.tryDecodeExactProviderTerminal(terminal({ error: { name: 'TimeoutError' } })), {
    sessionId: executionKey.sessionId,
    physicalUserMessageId: executionKey.physicalUserMessageId,
    providerRun: provider.providerRun,
    outcome: 'ProviderFailure',
    failure: 'ProviderTransient',
    disposition: '',
  })
  assert.deepEqual(
    await recovery.interpretFailurePolicy(
      'ProviderTransient',
      'Available',
      'Available',
      'NotCommitted',
      'ExactAbsent',
    ),
    { decision: 'RequeueEligible', effects: ['RequeueEligible:RetryFreshAttempt'] },
  )

  const permanentFallback = decide('ProviderPermanent')
  assertSingleRecovery(permanentFallback, 'AdvanceFallback')
  assert.deepEqual(hostSignals.tryDecodeExactProviderTerminal(terminal({ error: { name: 'ProviderError' } })), {
    sessionId: executionKey.sessionId,
    physicalUserMessageId: executionKey.physicalUserMessageId,
    providerRun: provider.providerRun,
    outcome: 'ProviderFailure',
    failure: 'ProviderPermanent',
    disposition: '',
  })
  const permanentTerminal = decide('ProviderPermanent', {
    provider: { ...provider, fallbackBudget: 'Exhausted' },
  })
  assertNoRecovery(permanentTerminal)
  assert.equal(permanentTerminal.messageDisposition.disposition, 'Failed')
  assert.deepEqual(
    await recovery.interpretFailurePolicy(
      'ProviderPermanent',
      'Available',
      'Exhausted',
      'NotCommitted',
      'ExactAbsent',
    ),
    { decision: 'Finalize', effects: ['Finalize:Failed'] },
  )

  assert.deepEqual(hostSignals.tryDecodeExactProviderTerminal(terminal({ finish: 'content-filter' })), {
    sessionId: executionKey.sessionId,
    physicalUserMessageId: executionKey.physicalUserMessageId,
    providerRun: provider.providerRun,
    outcome: 'ContentFiltered',
    failure: '',
    disposition: 'Completed',
  })
  assert.deepEqual(await recovery.recoverScenarios(['ProviderTerminalCompleted']), {
    decisions: ['Finalize'],
    effects: ['Finalize:Completed'],
  })

  const superseded = decide('Superseded')
  assertNoRecovery(superseded)
  assert.equal(superseded.messageDisposition.disposition, 'Cancelled')
  assert.deepEqual(superseded.messageDisposition.executionKey, executionKey)
  assert.equal(superseded.capacitySettlement.fenceReference, capacityFence.reference)
  const newer = decide('Superseded', {
    executionKey: { sessionId: executionKey.sessionId, physicalUserMessageId: 'msg-newer' },
    capacityFence: { reference: 'fence-newer' },
  })
  assert.notEqual(superseded.capacitySettlement.fenceReference, newer.capacitySettlement.fenceReference)
  const supersededAdmission = await transaction.transactionScenario(
    transactionEvidence('superseded'),
    'AcquireSuperseded',
    'None',
  )
  assert.equal(supersededAdmission.outcome, 'Superseded')
  assert.equal(supersededAdmission.providerCount, 0)
  assert.equal(supersededAdmission.admission.activeCapacity, 0)

  const interrupted = decide('StreamInterruptedAfterFirstToken')
  assertNoRecovery(interrupted)
  assert.equal(interrupted.messageDisposition.disposition, 'Failed')
  assert.deepEqual(hostSignals.tryDecodeExactProviderTerminal(terminal({ error: { name: 'StreamInterruptedError' } })), {
    sessionId: executionKey.sessionId,
    physicalUserMessageId: executionKey.physicalUserMessageId,
    providerRun: provider.providerRun,
    outcome: 'Interrupted',
    failure: 'StreamInterruptedAfterFirstToken',
    disposition: 'Failed',
  })
  assert.deepEqual(
    await recovery.interpretFailurePolicy(
      'StreamInterruptedAfterFirstToken',
      'Available',
      'Available',
      'NotCommitted',
      'ExactAbsent',
    ),
    { decision: 'Finalize', effects: ['Finalize:Failed'] },
  )

  const queueFull = decide('CapacityQueueFull', { phase: 'AcceptedBeforeProvider' })
  assertNoRecovery(queueFull)
  assert.equal(queueFull.messageDisposition.disposition, 'Failed')
  const fullAdmission = await transaction.transactionScenario(
    transactionEvidence('queue-full'),
    'AcquireQueueFull',
    'None',
  )
  assert.equal(fullAdmission.outcome, 'CapacityQueueFull')
  assert.equal(fullAdmission.providerCount, 0)
  assert.equal(fullAdmission.acquireCount, 1)
  assert.equal(fullAdmission.bindCount, 0)
  assert.equal(fullAdmission.admission.activeCapacity, 0)

  const invariant = decide('LocalInvariant')
  assertNoRecovery(invariant)
  assert.equal(invariant.messageDisposition.disposition, 'Failed')
  assert.equal(invariant.capacitySettlement.kind, 'ReleaseExactFence')
  assert.equal(invariant.fatality.kind, 'FatalAfterSettlement')
  assert.deepEqual(
    await recovery.interpretFailurePolicy(
      'LocalInvariant',
      'Available',
      'Available',
      'NotCommitted',
      'ExactAbsent',
    ),
    { decision: 'Finalize', effects: ['Finalize:Failed'] },
  )
  assert.equal(hooks.hookFailurePolicy('LocalInvariant', 'SettlementIncomplete'), 'RejectFatalBeforeSettlement')
  const exactSettlement = await transaction.preProviderSettlementScenario(
    transactionEvidence('local-invariant'),
    'FatalMembraneInput',
    'Exact',
  )
  const fatal = hooks.hookFailurePolicy('LocalInvariant', 'ExactSettlementComplete')
  assert.deepEqual([...exactSettlement.trace, fatal].slice(-4), [
    'TerminalizeAccepted',
    'UnbindExecution',
    'ReleaseBeforeProvider',
    'FatalAfterSettlement',
  ])
  assert.equal(exactSettlement.admission.activeCapacity, 0)
})
