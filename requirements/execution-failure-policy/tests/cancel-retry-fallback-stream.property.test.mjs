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
    fallbackBudget: arbitraryBudget,
    breaker: arbitraryBreaker,
  }),
})

const isRecoveryResolution = (resolution) =>
  resolution === 'RetryFreshAttempt' || resolution === 'AdvanceFallback'

const isTerminalResolution = (resolution) =>
  resolution === 'TerminalizeAcceptedPreProvider' || resolution === 'TerminalizeProviderStarted'

test('WHAT[EXECFAIL-003] finite provider budget matrix fixes policy and recovery outcomes', async () => {
  // Dense model property: temporal invariants over arbitrary failure, phase, budgets, breaker, capacity
  fc.assert(
    fc.property(arbitraryExecutionFailureInput, (input) => {
      const decision = policy.decide(input)

      // Invariant 1: Exactly one resolution axis
      assert.equal(typeof decision.resolution, 'string')
      assert.ok([
        'PreserveCurrentFact',
        'AwaitAcceptanceReconciliation',
        'RetryFreshAttempt',
        'AdvanceFallback',
        'TerminalizeAcceptedPreProvider',
        'TerminalizeProviderStarted',
      ].includes(decision.resolution))

      // Invariant 2: Recover implies no terminal; authorization present iff recovery
      if (isRecoveryResolution(decision.resolution)) {
        assert.equal(decision.terminalDisposition, null)
        assert.ok(decision.authorization)
        assert.equal(decision.authorization.logicalRun, input.provider.logicalRun)
        assert.equal(decision.authorization.providerRun, input.provider.providerRun)
        assert.equal(decision.authorization.requestKind, input.provider.requestKind)
        assert.equal(typeof decision.authorization.decisionId, 'string')
        assert.notEqual(decision.authorization.decisionId, '')
      }

      // Invariant 3: Terminal implies no recover; terminalDisposition present iff terminal
      if (isTerminalResolution(decision.resolution)) {
        assert.equal(decision.authorization, null)
        assert.ok(['Completed', 'Cancelled', 'Rejected', 'Failed'].includes(decision.terminalDisposition))
        assert.deepEqual(decision.executionKey, executionKey)
      }

      // Invariant 4: Cancellation/protocol/local invariant/non-provider failure never recover
      const isProviderFailure =
        input.failure === 'ProviderTransient' || input.failure === 'ProviderPermanent'
      if (!isProviderFailure || input.phase !== 'ProviderStarted' || input.provider.requestKind === 'StrengthReplica') {
        assert.ok(!isRecoveryResolution(decision.resolution), `${input.failure}/${input.phase} must not recover`)
      }

      // Invariant 5: LocalInvariant / ProtocolRejection / UserCancelled / Superseded invariant rules
      if (input.failure === 'LocalInvariant' || input.failure === 'ProtocolRejection' || input.failure === 'AuthorizationDenied') {
        if (input.phase === 'AcceptedBeforeProvider') {
          assert.equal(decision.resolution, 'TerminalizeAcceptedPreProvider')
        } else if (input.phase === 'ProviderStarted') {
          assert.equal(decision.resolution, 'TerminalizeProviderStarted')
        }
      }

      // Invariant 6: Every provider-started confirmed failure produces recovery or terminal, never neither
      if (
        input.phase === 'ProviderStarted' &&
        (input.failure === 'ProviderTransient' || input.failure === 'ProviderPermanent')
      ) {
        assert.ok(
          isRecoveryResolution(decision.resolution) || isTerminalResolution(decision.resolution),
          'provider-started failure must produce recovery or terminal',
        )
      }

      // Invariant 7: Duplicate observation is deterministic and produces identical authorization decisionId
      const replay = policy.decide(input)
      assert.deepEqual(replay, decision)
    }),
    { seed: 0x45584543, numRuns: 200 },
  )

  // Property 2: Distinct provider runs create distinct authorization decisionId
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
            retryBudget: 'Exhausted',
            fallbackBudget: 'Available',
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
            retryBudget: 'Exhausted',
            fallbackBudget: 'Available',
          },
        })

        assert.equal(decisionA.resolution, 'AdvanceFallback')
        assert.equal(decisionB.resolution, 'AdvanceFallback')
        assert.notEqual(decisionA.authorization.decisionId, decisionB.authorization.decisionId)
      },
    ),
    { seed: 0x52554e49, numRuns: 100 },
  )

  // Recovery runtime interpretation: provider recovery is owned by ProviderRecoveryWorkflow,
  // so chat recovery produces Ignore with ProviderRecoveryOwned and zero effects.
  const transientOutcome = await recovery.interpretFailurePolicy(
    'ProviderTransient',
    'Available',
    'Available',
    'NotCommitted',
    'ExactAbsent',
  )
  assert.deepEqual(transientOutcome, { decision: 'Ignore', effects: [] })

  const fallbackOutcome = await recovery.interpretFailurePolicy(
    'ProviderPermanent',
    'Exhausted',
    'Available',
    'NotCommitted',
    'ExactAbsent',
  )
  assert.deepEqual(fallbackOutcome, { decision: 'Ignore', effects: [] })

  const terminalOutcome = await recovery.interpretFailurePolicy(
    'ProviderPermanent',
    'Exhausted',
    'Exhausted',
    'NotCommitted',
    'ExactAbsent',
  )
  assert.deepEqual(terminalOutcome, { decision: 'Finalize', effects: ['Finalize:Failed'] })

  // Exercise hook promises
  for (const label of ['transient', 'permanent', 'exhausted']) {
    const wrapped = hooks.policyAwareHook(`policy-matrix-${label}`, () => label)
    assert.equal(await wrapped('args', 'context'), label)

    const rejection = hooks.providerInputRejection(label)
    const failing = hooks.policyAwareHook(`policy-matrix-${label}`, () => Promise.reject(rejection))
    await assert.rejects(() => failing('args', 'context'), (error) => error === rejection)
  }
})
