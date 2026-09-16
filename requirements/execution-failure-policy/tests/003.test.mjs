import assert from 'node:assert/strict'
import test from 'node:test'
import fc from 'fast-check'

import * as policy from '../../../dist/Execution/Failure/Surface.js'
import * as recovery from '../../../dist/Execution/Session/ChatExecution/RecoveryRuntimeSurface.js'
import * as hooks from '../../../dist/OpenCode/Host/PluginHooksSurface.js'

const executionKey = {
  sessionId: 'ses-failure-property',
  physicalUserMessageId: 'msg-failure-property',
}
const capacityFence = { reference: 'fence-failure-property' }

const baseProvider = {
  logicalRun: 'logical-failure-property',
  providerRun: 'provider-failure-property',
  requestKind: 'WorkMain',
  breaker: 'Closed',
}

const arbitraryFailure = fc.constantFrom(
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
)

const arbitraryPhase = fc.constantFrom(
  'NoAcceptedFact',
  'AcceptedBeforeProvider',
  'ProviderStarted',
  'Terminal',
)

const arbitraryBudget = fc.constantFrom('Available', 'Exhausted')
const arbitraryBreaker = fc.constantFrom('Closed', 'Open')
const arbitraryRequestKind = fc.constantFrom(
  'WorkMain',
  'BloggerMain',
  'BloggerSquash',
  'InteractionRepair',
  'StrengthReplica',
)

const arbitraryCapacityFence = fc.constantFrom(null, capacityFence)

const arbitraryExecutionFailureInput = fc.record({
  failure: arbitraryFailure,
  phase: arbitraryPhase,
  executionKey: fc.constant(executionKey),
  capacityFence: arbitraryCapacityFence,
  provider: fc.record({
    logicalRun: fc.constant(baseProvider.logicalRun),
    providerRun: fc.string({ minLength: 1, maxLength: 32 }).map((suffix) => `provider-${suffix}`),
    requestKind: arbitraryRequestKind,
    retryBudget: arbitraryBudget,
    breaker: arbitraryBreaker,
  }),
})

const isRecoveryResolution = (resolution) =>
  resolution === 'RetryFreshAttempt'

const isTerminalResolution = (resolution) =>
  resolution === 'TerminalizeAcceptedPreProvider' || resolution === 'TerminalizeProviderStarted'

const nonProviderFailures = [
  'LocalInvariant',
  'ProtocolRejection',
  'AuthorizationDenied',
  'UserCancelled',
  'Superseded',
  'CapacityQueueFull',
  'AcceptanceUnknown',
  'StreamInterruptedAfterFirstToken',
  { kind: 'PersistenceFailure', commitment: 'NotCommitted' },
  { kind: 'PersistenceFailure', commitment: 'Committed' },
  { kind: 'PersistenceFailure', commitment: 'Unknown' },
]

const providerCases = [
  {
    label: 'transient retry on Closed and Available',
    failure: 'ProviderTransient',
    facts: { ...baseProvider, retryBudget: 'Available' },
    resolution: 'RetryFreshAttempt',
    breaker: 'RecordProviderTransientFailure',
  },
  {
    label: 'transient terminal after retry exhaustion',
    failure: 'ProviderTransient',
    facts: { ...baseProvider, retryBudget: 'Exhausted' },
    resolution: 'TerminalizeProviderStarted',
    breaker: 'RecordProviderTransientFailure',
  },
  {
    label: 'transient terminal around an open breaker',
    failure: 'ProviderTransient',
    facts: { ...baseProvider, retryBudget: 'Available', breaker: 'Open' },
    resolution: 'TerminalizeProviderStarted',
    breaker: 'RecordProviderTransientFailure',
  },
  {
    label: 'permanent retry on Closed and Available',
    failure: 'ProviderPermanent',
    facts: { ...baseProvider, retryBudget: 'Available' },
    resolution: 'RetryFreshAttempt',
    breaker: 'RecordProviderPermanentFailure',
  },
  {
    label: 'permanent terminal after retry exhaustion',
    failure: 'ProviderPermanent',
    facts: { ...baseProvider, retryBudget: 'Exhausted' },
    resolution: 'TerminalizeProviderStarted',
    breaker: 'RecordProviderPermanentFailure',
  },
  {
    label: 'permanent terminal around an open breaker',
    failure: 'ProviderPermanent',
    facts: { ...baseProvider, retryBudget: 'Available', breaker: 'Open' },
    resolution: 'TerminalizeProviderStarted',
    breaker: 'RecordProviderPermanentFailure',
  },
  ...['BloggerMain', 'BloggerSquash', 'InteractionRepair'].map((requestKind) => ({
    label: `${requestKind} remains provider-recoverable`,
    failure: 'ProviderTransient',
    facts: { ...baseProvider, requestKind, retryBudget: 'Available' },
    resolution: 'RetryFreshAttempt',
    breaker: 'RecordProviderTransientFailure',
  })),
  {
    label: 'StrengthReplica cannot consume owner recovery',
    failure: 'ProviderTransient',
    facts: { ...baseProvider, requestKind: 'StrengthReplica', retryBudget: 'Available' },
    resolution: 'TerminalizeProviderStarted',
    breaker: 'RecordProviderTransientFailure',
  },
]

test('WHAT[EXECFAIL-003] finite provider budget matrix fixes policy and recovery outcomes', async () => {
  fc.assert(
    fc.property(arbitraryExecutionFailureInput, (input) => {
      const decision = policy.decide(input)

      assert.equal(typeof decision.resolution, 'string')
      assert.ok([
        'PreserveCurrentFact',
        'AwaitAcceptanceReconciliation',
        'RetryFreshAttempt',
        'TerminalizeAcceptedPreProvider',
        'TerminalizeProviderStarted',
      ].includes(decision.resolution))

      if (isRecoveryResolution(decision.resolution)) {
        assert.equal(decision.terminalDisposition, null)
        assert.ok(decision.authorization)
        assert.equal(decision.authorization.logicalRun, input.provider.logicalRun)
        assert.equal(decision.authorization.providerRun, input.provider.providerRun)
        assert.equal(decision.authorization.requestKind, input.provider.requestKind)
        assert.equal(typeof decision.authorization.decisionId, 'string')
        assert.notEqual(decision.authorization.decisionId, '')
      } else {
        assert.equal(decision.authorization, null)
      }

      if (isTerminalResolution(decision.resolution)) {
        assert.equal(decision.authorization, null)
        assert.ok(['Completed', 'Cancelled', 'Rejected', 'Failed'].includes(decision.terminalDisposition))
        assert.deepEqual(decision.executionKey, executionKey)
      }

      const isProviderFailure =
        input.failure === 'ProviderTransient' || input.failure === 'ProviderPermanent'
      if (!isProviderFailure || input.phase !== 'ProviderStarted' || input.provider.requestKind === 'StrengthReplica') {
        assert.ok(!isRecoveryResolution(decision.resolution), `${JSON.stringify(input.failure)}/${input.phase} must not recover`)
      }

      if (input.provider.breaker === 'Open' || input.provider.retryBudget === 'Exhausted') {
        assert.ok(!isRecoveryResolution(decision.resolution), 'open or exhausted single budget must not recover')
      }

      if (
        isProviderFailure &&
        input.phase === 'ProviderStarted' &&
        input.provider.requestKind !== 'StrengthReplica' &&
        input.provider.breaker === 'Closed' &&
        input.provider.retryBudget === 'Available'
      ) {
        assert.equal(decision.resolution, 'RetryFreshAttempt')
      }

      if (input.failure === 'LocalInvariant' || input.failure === 'ProtocolRejection' || input.failure === 'AuthorizationDenied') {
        if (input.phase === 'AcceptedBeforeProvider') {
          assert.equal(decision.resolution, 'TerminalizeAcceptedPreProvider')
        } else if (input.phase === 'ProviderStarted') {
          assert.equal(decision.resolution, 'TerminalizeProviderStarted')
        }
      }

      if (
        input.phase === 'ProviderStarted' &&
        (input.failure === 'ProviderTransient' || input.failure === 'ProviderPermanent')
      ) {
        assert.ok(
          isRecoveryResolution(decision.resolution) || isTerminalResolution(decision.resolution),
          'provider-started failure must produce recovery or terminal',
        )
      }

      const replay = policy.decide(input)
      assert.deepEqual(replay, decision)
    }),
    { seed: 0x45584543, numRuns: 200 },
  )

  fc.assert(
    fc.property(
      fc.tuple(
        fc.string({ minLength: 1, maxLength: 16 }),
        fc.string({ minLength: 1, maxLength: 16 }),
      ).filter(([a, b]) => a !== b),
      ([runA, runB]) => {
        const decisionA = policy.decide({
          failure: 'ProviderPermanent',
          phase: 'ProviderStarted',
          executionKey,
          capacityFence,
          provider: {
            ...baseProvider,
            providerRun: `provider-${runA}`,
            retryBudget: 'Available',
          },
        })
        const decisionB = policy.decide({
          failure: 'ProviderPermanent',
          phase: 'ProviderStarted',
          executionKey,
          capacityFence,
          provider: {
            ...baseProvider,
            providerRun: `provider-${runB}`,
            retryBudget: 'Available',
          },
        })

        assert.equal(decisionA.resolution, 'RetryFreshAttempt')
        assert.equal(decisionB.resolution, 'RetryFreshAttempt')
        assert.notEqual(decisionA.authorization.decisionId, decisionB.authorization.decisionId)
      },
    ),
    { seed: 0x52554e49, numRuns: 100 },
  )

  const transientOutcome = await recovery.interpretFailurePolicy(
    'ProviderTransient',
    'Available',
    'NotCommitted',
    'ExactAbsent',
  )
  assert.deepEqual(transientOutcome, { decision: 'Ignore', effects: [] })

  const permanentOutcome = await recovery.interpretFailurePolicy(
    'ProviderPermanent',
    'Available',
    'NotCommitted',
    'ExactAbsent',
  )
  assert.deepEqual(permanentOutcome, { decision: 'Ignore', effects: [] })

  const terminalOutcome = await recovery.interpretFailurePolicy(
    'ProviderPermanent',
    'Exhausted',
    'NotCommitted',
    'ExactAbsent',
  )
  assert.deepEqual(terminalOutcome, { decision: 'Finalize', effects: ['Finalize:Failed'] })

  for (const label of ['transient', 'permanent', 'exhausted']) {
    const wrapped = hooks.policyAwareHook(`policy-matrix-${label}`, () => label)
    assert.equal(await wrapped('args', 'context'), label)

    const rejection = hooks.providerInputRejection(label)
    const failing = hooks.policyAwareHook(`policy-matrix-${label}`, () => Promise.reject(rejection))
    await assert.rejects(() => failing('args', 'context'), (error) => error === rejection)
  }
})

test('WHAT[EXECFAIL-003] rejects illegal retry and breaker policy mutations', () => {
  const baseInput = {
    failure: 'ProtocolRejection',
    phase: 'ProviderStarted',
    executionKey,
    capacityFence,
    provider: { ...baseProvider, retryBudget: 'Available' },
  }
  const decide = (change = {}) => policy.decide({ ...baseInput, ...change })

  for (const failure of nonProviderFailures) {
    const decision = decide({ failure })
    assert.notEqual(decision.resolution, 'RetryFreshAttempt')
    assert.equal(decision.breaker.kind, 'NoBreakerTransition')
  }

  for (const scenario of providerCases) {
    const decision = decide({ failure: scenario.failure, provider: scenario.facts })
    assert.equal(decision.resolution, scenario.resolution, scenario.label)
    assert.equal(decision.breaker.kind, scenario.breaker, scenario.label)

    if (decision.resolution === 'RetryFreshAttempt') {
      const authorization = decision.authorization
      assert.ok(authorization)
      assert.equal(authorization.providerRun, scenario.facts.providerRun)
      assert.equal(authorization.logicalRun, scenario.facts.logicalRun)
      assert.equal(authorization.requestKind, scenario.facts.requestKind)
      assert.equal(typeof authorization.decisionId, 'string')
      assert.notEqual(authorization.decisionId, '')
    } else {
      assert.equal(decision.authorization, null, scenario.label)
    }
  }

  const first = decide({ failure: 'ProviderPermanent' }).authorization
  const duplicate = decide({ failure: 'ProviderPermanent' }).authorization
  const freshAttempt = decide({
    failure: 'ProviderPermanent',
    provider: { ...baseProvider, retryBudget: 'Available', providerRun: 'provider-failure-policy-2' },
  }).authorization
  assert.equal(first.decisionId, duplicate.decisionId)
  assert.notEqual(first.decisionId, freshAttempt.decisionId)

  const wrongPhase = decide({ failure: 'ProviderTransient', phase: 'AcceptedBeforeProvider' })
  assert.notEqual(wrongPhase.resolution, 'RetryFreshAttempt')

  const openBreaker = decide({ failure: 'ProviderPermanent', provider: { ...baseProvider, retryBudget: 'Available', breaker: 'Open' } })
  assert.notEqual(openBreaker.resolution, 'RetryFreshAttempt')

  const exhausted = decide({ failure: 'ProviderPermanent', provider: { ...baseProvider, retryBudget: 'Exhausted' } })
  assert.notEqual(exhausted.resolution, 'RetryFreshAttempt')
})
