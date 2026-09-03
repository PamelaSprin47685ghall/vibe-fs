import assert from 'node:assert/strict'
import test from 'node:test'

import * as policy from '../../../dist/Execution/Failure/Surface.js'
import * as recovery from '../../../dist/Execution/Session/ChatExecution/RecoveryRuntimeSurface.js'
import * as hooks from '../../../dist/OpenCode/Host/PluginHooksSurface.js'

const executionKey = {
  sessionId: 'ses-failure-property',
  physicalUserMessageId: 'msg-failure-property',
}
const capacityFence = { reference: 'fence-failure-property' }
const providerBase = {
  logicalRun: 'logical-failure-property',
  providerRun: 'provider-failure-property',
  requestKind: 'WorkMain',
  retryBudget: 'Available',
  fallbackBudget: 'Available',
  breaker: 'Closed',
}

const providerCases = [
  {
    label: 'transient/available/available',
    failure: 'ProviderTransient',
    retryBudget: 'Available',
    fallbackBudget: 'Available',
    retry: 'RetryFreshAttempt',
    fallback: 'NoFallback',
    breaker: 'RecordProviderTransientFailure',
    messageDisposition: 'KeepCurrentFact',
    recovery: {
      decision: 'RequeueEligible',
      effects: ['RequeueEligible:RetryFreshAttempt'],
    },
  },
  {
    label: 'transient/available/exhausted',
    failure: 'ProviderTransient',
    retryBudget: 'Available',
    fallbackBudget: 'Exhausted',
    retry: 'RetryFreshAttempt',
    fallback: 'NoFallback',
    breaker: 'RecordProviderTransientFailure',
    messageDisposition: 'KeepCurrentFact',
    recovery: {
      decision: 'RequeueEligible',
      effects: ['RequeueEligible:RetryFreshAttempt'],
    },
  },
  {
    label: 'transient/exhausted/available',
    failure: 'ProviderTransient',
    retryBudget: 'Exhausted',
    fallbackBudget: 'Available',
    retry: 'NoRetry',
    fallback: 'AdvanceFallback',
    breaker: 'RecordProviderTransientFailure',
    messageDisposition: 'KeepCurrentFact',
    recovery: {
      decision: 'RequeueEligible',
      effects: ['RequeueEligible:AdvanceFallback'],
    },
  },
  {
    label: 'transient/exhausted/exhausted',
    failure: 'ProviderTransient',
    retryBudget: 'Exhausted',
    fallbackBudget: 'Exhausted',
    retry: 'NoRetry',
    fallback: 'NoFallback',
    breaker: 'RecordProviderTransientFailure',
    messageDisposition: 'TerminalizeProviderStarted',
    recovery: { decision: 'Finalize', effects: ['Finalize:Failed'] },
  },
  {
    label: 'permanent/available/available',
    failure: 'ProviderPermanent',
    retryBudget: 'Available',
    fallbackBudget: 'Available',
    retry: 'NoRetry',
    fallback: 'AdvanceFallback',
    breaker: 'RecordProviderPermanentFailure',
    messageDisposition: 'KeepCurrentFact',
    recovery: {
      decision: 'RequeueEligible',
      effects: ['RequeueEligible:AdvanceFallback'],
    },
  },
  {
    label: 'permanent/exhausted/available',
    failure: 'ProviderPermanent',
    retryBudget: 'Exhausted',
    fallbackBudget: 'Available',
    retry: 'NoRetry',
    fallback: 'AdvanceFallback',
    breaker: 'RecordProviderPermanentFailure',
    messageDisposition: 'KeepCurrentFact',
    recovery: {
      decision: 'RequeueEligible',
      effects: ['RequeueEligible:AdvanceFallback'],
    },
  },
  {
    label: 'permanent/available/exhausted',
    failure: 'ProviderPermanent',
    retryBudget: 'Available',
    fallbackBudget: 'Exhausted',
    retry: 'NoRetry',
    fallback: 'NoFallback',
    breaker: 'RecordProviderPermanentFailure',
    messageDisposition: 'TerminalizeProviderStarted',
    recovery: { decision: 'Finalize', effects: ['Finalize:Failed'] },
  },
  {
    label: 'permanent/exhausted/exhausted',
    failure: 'ProviderPermanent',
    retryBudget: 'Exhausted',
    fallbackBudget: 'Exhausted',
    retry: 'NoRetry',
    fallback: 'NoFallback',
    breaker: 'RecordProviderPermanentFailure',
    messageDisposition: 'TerminalizeProviderStarted',
    recovery: { decision: 'Finalize', effects: ['Finalize:Failed'] },
  },
]

const fixedRecoveryCases = [
  {
    persistence: 'NotCommitted',
    observation: 'ExactTerminal',
    expected: { decision: 'Finalize', effects: ['Finalize:Failed'] },
  },
  {
    persistence: 'NotCommitted',
    observation: 'LateOldExecution',
    expected: { decision: 'Ignore', effects: [] },
  },
  ...['ExactAbsent', 'ExactTerminal', 'LateOldExecution'].map((observation) => ({
    persistence: 'Committed',
    observation,
    expected: { decision: 'Ignore', effects: [] },
  })),
  ...['ExactAbsent', 'ExactTerminal', 'LateOldExecution'].map((observation) => ({
    persistence: 'Unknown',
    observation,
    expected: {
      decision: 'MarkManualIntervention',
      effects: ['MarkManualIntervention:PersistenceOutcomeUnknown'],
    },
  })),
]

const decide = ({ failure, retryBudget, fallbackBudget }) =>
  policy.decide({
    failure,
    phase: 'ProviderStarted',
    executionKey,
    capacityFence,
    provider: { ...providerBase, retryBudget, fallbackBudget },
  })

const assertAuthorization = (action, providerCase) => {
  assert.equal(action.logicalRun, providerBase.logicalRun, providerCase.label)
  assert.equal(action.providerRun, providerBase.providerRun, providerCase.label)
  assert.equal(action.requestKind, providerBase.requestKind, providerCase.label)
  assert.equal(typeof action.decisionId, 'string', providerCase.label)
  assert.notEqual(action.decisionId, '', providerCase.label)
}

const assertPolicyOutcome = (providerCase, decision) => {
  assert.equal(decision.retry.kind, providerCase.retry, providerCase.label)
  assert.equal(decision.fallback.kind, providerCase.fallback, providerCase.label)
  assert.equal(decision.breaker.kind, providerCase.breaker, providerCase.label)
  assert.deepEqual(
    decision.capacitySettlement,
    { kind: 'ReleaseExactFence', fenceReference: capacityFence.reference },
    providerCase.label,
  )
  assert.equal(
    decision.messageDisposition.kind,
    providerCase.messageDisposition,
    providerCase.label,
  )
  assert.equal(decision.fatality.kind, 'NoFatality', providerCase.label)

  if (providerCase.retry === 'RetryFreshAttempt') {
    assertAuthorization(decision.retry, providerCase)
  }
  if (providerCase.fallback === 'AdvanceFallback') {
    assertAuthorization(decision.fallback, providerCase)
  }
  if (providerCase.messageDisposition === 'TerminalizeProviderStarted') {
    assert.equal(decision.messageDisposition.disposition, 'Failed', providerCase.label)
    assert.deepEqual(decision.messageDisposition.executionKey, executionKey, providerCase.label)
  }
}

const interpret = (providerCase, persistence, observation) =>
  recovery.interpretFailurePolicy(
    providerCase.failure,
    providerCase.retryBudget,
    providerCase.fallbackBudget,
    persistence,
    observation,
  )

const exerciseHookPromise = async (mode, label) => {
  if (mode === 'Fulfilled') {
    const wrapped = hooks.policyAwareHook(`policy-matrix-${label}`, () => label)
    const promise = wrapped('args', 'context')
    assert.equal(typeof promise.then, 'function')
    assert.equal(await promise, label)
    return
  }

  const rejection = hooks.providerInputRejection(label)
  const wrapped = hooks.policyAwareHook(`policy-matrix-${label}`, () => Promise.reject(rejection))
  const promise = wrapped('args', 'context')
  assert.equal(typeof promise.then, 'function')
  await assert.rejects(() => promise, (error) => error === rejection)
}

test('WHAT[EXECFAIL-003] finite provider budget matrix fixes policy and recovery outcomes', async () => {
  for (const providerCase of providerCases) {
    const decision = decide(providerCase)
    assertPolicyOutcome(providerCase, decision)
    assert.deepEqual(decide(providerCase), decision, `${providerCase.label}: unstable policy decision`)

    assert.deepEqual(
      await interpret(providerCase, 'NotCommitted', 'ExactAbsent'),
      providerCase.recovery,
      providerCase.label,
    )
    assert.deepEqual(
      await interpret(providerCase, 'NotCommitted', 'ExactAbsent'),
      providerCase.recovery,
      `${providerCase.label}: duplicate evidence changed fixed outcome`,
    )

    for (const recoveryCase of fixedRecoveryCases) {
      assert.deepEqual(
        await interpret(providerCase, recoveryCase.persistence, recoveryCase.observation),
        recoveryCase.expected,
        `${providerCase.label}/${recoveryCase.persistence}/${recoveryCase.observation}`,
      )
    }

    await exerciseHookPromise('Fulfilled', providerCase.label)
    await exerciseHookPromise('TypedProtocolRejection', providerCase.label)
  }
})
