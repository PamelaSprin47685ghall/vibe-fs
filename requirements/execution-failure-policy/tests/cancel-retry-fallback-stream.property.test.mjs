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
const retryBudgetCases = [
  { label: 'zero', state: 'Exhausted' },
  { label: 'boundary-one-remains', state: 'Available' },
  { label: 'exhausted', state: 'Exhausted' },
]
const fallbackCases = ['Available', 'Exhausted']
const persistenceCases = ['Committed', 'NotCommitted', 'Unknown']
const evidenceCases = [
  { label: 'exact', observation: 'ExactAbsent', duplicate: false },
  { label: 'duplicate-exact', observation: 'ExactAbsent', duplicate: true },
  { label: 'late-old', observation: 'LateOldExecution', duplicate: false },
]
const hookCases = ['Fulfilled', 'TypedProtocolRejection']
const providerFailures = ['ProviderTransient', 'ProviderPermanent']

const decide = (failure, retryBudget, fallbackBudget) => policy.decide({
  failure,
  phase: 'ProviderStarted',
  executionKey,
  capacityFence,
  provider: { ...providerBase, retryBudget, fallbackBudget },
})

const licensedActions = (decision) => [decision.retry, decision.fallback]
  .filter((action) => action.kind !== 'NoRetry' && action.kind !== 'NoFallback')

const expectedRecoveryEffect = (decision) => {
  if (decision.retry.kind === 'RetryFreshAttempt') return 'RequeueEligible:RetryFreshAttempt'
  if (decision.fallback.kind === 'AdvanceFallback') return 'RequeueEligible:AdvanceFallback'
  if (decision.messageDisposition.kind === 'TerminalizeProviderStarted') {
    return `Finalize:${decision.messageDisposition.disposition}`
  }
  return 'MarkManualIntervention:NoAuthorizedProviderDisposition'
}

const assertCoherent = (failure, decision) => {
  assert.ok(licensedActions(decision).length <= 1, 'one policy decision licenses at most one owner action')
  assert.equal(decision.capacitySettlement.kind, 'ReleaseExactFence')
  assert.equal(decision.capacitySettlement.fenceReference, capacityFence.reference)
  assert.equal(decision.fatality.kind, failure === 'LocalInvariant' ? 'FatalAfterSettlement' : 'NoFatality')
  if (failure !== 'ProviderTransient' && failure !== 'ProviderPermanent') {
    assert.equal(decision.retry.kind, 'NoRetry')
    assert.equal(decision.fallback.kind, 'NoFallback')
  }
  if (decision.retry.kind === 'RetryFreshAttempt') assert.equal(failure, 'ProviderTransient')
  if (decision.fallback.kind === 'AdvanceFallback') {
    assert.ok(failure === 'ProviderTransient' || failure === 'ProviderPermanent')
  }
}

const exerciseHookPromise = async (mode, label) => {
  if (mode === 'Fulfilled') {
    const wrapped = hooks.policyAwareHook(`policy-property-${label}`, () => label)
    const promise = wrapped('args', 'context')
    assert.equal(typeof promise.then, 'function')
    assert.equal(await promise, label)
    return
  }

  const rejection = hooks.providerInputRejection(label)
  const wrapped = hooks.policyAwareHook(`policy-property-${label}`, () => Promise.reject(rejection))
  const promise = wrapped('args', 'context')
  assert.equal(typeof promise.then, 'function')
  await assert.rejects(() => promise, (error) => error === rejection)
}

test('WHAT[EXECFAIL-003] generated budgets, fallback, persistence, Host evidence, and Hook Promises preserve one owner licence', async () => {
  let combinations = 0

  for (const failure of providerFailures) {
    for (const retryBudget of retryBudgetCases) {
      for (const fallbackBudget of fallbackCases) {
        for (const persistence of persistenceCases) {
          for (const evidence of evidenceCases) {
            for (const hookMode of hookCases) {
              const label = [
                failure,
                retryBudget.label,
                fallbackBudget,
                persistence,
                evidence.label,
                hookMode,
              ].join('/')
              const decision = decide(failure, retryBudget.state, fallbackBudget)
              assertCoherent(failure, decision)

              const interpreted = await recovery.interpretFailurePolicy(
                failure,
                retryBudget.state,
                fallbackBudget,
                persistence,
                evidence.observation,
              )
              assert.ok(interpreted.effects.length <= 1, `${label}: interpreter emitted multiple owner actions`)

              if (persistence === 'Committed') {
                assert.deepEqual(interpreted, { decision: 'Ignore', effects: [] }, label)
              } else if (persistence === 'Unknown') {
                assert.deepEqual(
                  interpreted,
                  {
                    decision: 'MarkManualIntervention',
                    effects: ['MarkManualIntervention:PersistenceOutcomeUnknown'],
                  },
                  label,
                )
              } else if (evidence.observation === 'LateOldExecution') {
                assert.deepEqual(interpreted, { decision: 'Ignore', effects: [] }, label)
              } else {
                assert.deepEqual(
                  interpreted,
                  { decision: licensedActions(decision).length === 0 ? 'Finalize' : 'RequeueEligible', effects: [expectedRecoveryEffect(decision)] },
                  label,
                )
              }

              if (evidence.duplicate) {
                const duplicate = await recovery.interpretFailurePolicy(
                  failure,
                  retryBudget.state,
                  fallbackBudget,
                  persistence,
                  evidence.observation,
                )
                assert.deepEqual(duplicate, interpreted, `${label}: duplicate evidence changed interpretation`)
                const repeatedDecision = decide(failure, retryBudget.state, fallbackBudget)
                assert.deepEqual(repeatedDecision, decision, `${label}: duplicate evidence minted a new licence`)
              }

              await exerciseHookPromise(hookMode, label)
              combinations += 1
            }
          }
        }
      }
    }
  }

  assert.equal(combinations, 216)
})

test('WHAT[EXECFAIL-006] retry, fatal, and exact-release mutations violate closed policy laws', () => {
  const transient = decide('ProviderTransient', 'Available', 'Available')
  assert.doesNotThrow(() => assertCoherent('ProviderTransient', transient))
  assert.throws(
    () => assertCoherent('UserCancelled', { ...decide('UserCancelled', 'Available', 'Available'), retry: transient.retry }),
    /one policy decision licenses at most one owner action|Expected values to be strictly equal/,
  )

  const invariant = decide('LocalInvariant', 'Exhausted', 'Exhausted')
  assert.doesNotThrow(() => assertCoherent('LocalInvariant', invariant))
  assert.throws(
    () => assertCoherent('LocalInvariant', { ...invariant, fatality: { kind: 'NoFatality' } }),
    /Expected values to be strictly equal/,
  )

  const cancelled = decide('UserCancelled', 'Exhausted', 'Exhausted')
  assert.doesNotThrow(() => assertCoherent('UserCancelled', cancelled))
  assert.throws(
    () => assertCoherent('UserCancelled', { ...cancelled, capacitySettlement: { kind: 'RetainExactFence', fenceReference: capacityFence.reference } }),
    /Expected values to be strictly equal/,
  )
})
