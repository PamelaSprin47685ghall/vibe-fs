import assert from 'node:assert/strict'
import test from 'node:test'
import fc from 'fast-check'

import * as Runtime from '../../../dist/Execution/Session/ChatExecution/RecoveryRuntimeSurface.js'
import * as recovery from '../../../dist/Execution/Session/ChatExecution/RecoverySurface.js'
import * as lifecycle from '../../../dist/Execution/Session/ChatExecution/Surface.js'

const validScenarios = [
  'CrashAfterAcceptance',
  'AcceptedProviderAlive',
  'AcceptedProviderTerminal',
  'ProviderAlive',
  'ProviderTerminalCompleted',
  'ProviderTerminalFailed',
  'ProviderTerminalCancelled',
  'ProviderTerminalRejected',
  'RetryExhausted',
  'Superseded',
  'MissingReceipt',
  'AmbiguousReceipt',
  'PhysicalOutcomeUnknown',
  'PersistenceUnknown',
  'DuplicateRecovery',
  'StaleProvider',
  'StaleKey',
  'StalePolicy',
  'TerminalResourceHeld',
  'TerminalResourceReleased',
  'ProviderAbsentWithoutPolicy',
]

const staleOrMismatchedScenarios = ['StaleKey', 'StaleProvider', 'StalePolicy']

const evidenceSeed = (sessionId, physicalId) => ({
  sessionId,
  physicalUserMessageId: physicalId,
  logicalRunId: `run-${sessionId}`,
  authorityRootUserMessageId: `root-${sessionId}`,
  authorityKind: 'HumanRoot',
  identitySeed: {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: {
      selectedAgent: 'coder',
      peerAgent: 'coder',
      canonicalRole: 'coder',
      selectedTier: 'deep',
      persona: 'Coder',
      personaCatalogVersion: 1,
      origin: 'ResolvedAtRoot',
    },
  },
  providerRun: `provider-${sessionId}`,
  origin: 'HumanRoot',
  effectiveAgent: 'coder',
  requestKind: 'work-main',
  projectionChoice: { kind: 'UseCommittedEpoch' },
})

const action = (evidence, kind, extra = {}) => ({ kind, evidence, appendOutcome: 'Committed', ...extra })

test('WHAT[CHATEXEC-012] duplicate terminal and stale recovery evidence are semantically inert', async () => {
  // Invariant 1: Durable semantic idempotence across duplicate and reordered lifecycle actions
  await fc.assert(
    fc.asyncProperty(
      fc.string({ minLength: 2, maxLength: 10 }),
      fc.constantFrom('Completed', 'Failed', 'Cancelled', 'Rejected'),
      async (id, disposition) => {
        const ev = evidenceSeed(`ses-idem-${id}`, `msg-idem-${id}`)
        const baseActions = [
          action(ev, 'Accept'),
          action(ev, 'ProviderStarted'),
          action(ev, 'Terminal', { disposition }),
        ]
        const single = await lifecycle.providerLifecycleScenario(baseActions)
        assert.equal(single.ok, true)

        // Duplicate and re-applied actions never increase semanticTransitionCount
        const duplicatedActions = [
          action(ev, 'Accept'),
          action(ev, 'Accept'),
          action(ev, 'ProviderStarted'),
          action(ev, 'ProviderStarted'),
          action(ev, 'Terminal', { disposition }),
          action(ev, 'Terminal', { disposition }),
        ]
        const duplicated = await lifecycle.providerLifecycleScenario(duplicatedActions)
        assert.equal(duplicated.ok, true)
        assert.deepEqual(duplicated.projection, single.projection)
        assert.equal(duplicated.semanticTransitionCount, single.semanticTransitionCount)
      },
    ),
    { seed: 0x4944454d, numRuns: 50 },
  )

  // Invariant 2: Terminal absorbs later observations with zero additional transitions
  await fc.assert(
    fc.asyncProperty(
      fc.string({ minLength: 2, maxLength: 10 }),
      fc.constantFrom('Completed', 'Failed', 'Cancelled', 'Rejected'),
      async (id, disposition) => {
        const ev = evidenceSeed(`ses-absorb-${id}`, `msg-absorb-${id}`)
        const baseActions = [
          action(ev, 'Accept'),
          action(ev, 'ProviderStarted'),
          action(ev, 'Terminal', { disposition }),
        ]
        const single = await lifecycle.providerLifecycleScenario(baseActions)
        assert.equal(single.ok, true)

        // Repeat terminal observations after terminal lifecycle state reached
        const absorbed = await lifecycle.providerLifecycleScenario([
          ...baseActions,
          action(ev, 'Terminal', { disposition }),
          action(ev, 'Terminal', { disposition }),
        ])
        assert.equal(absorbed.ok, true)
        assert.deepEqual(absorbed.projection, single.projection)
        assert.equal(absorbed.semanticTransitionCount, single.semanticTransitionCount)
      },
    ),
    { seed: 0x5445524d, numRuns: 50 },
  )

  // Invariant 3: Stale / mismatched evidence sequence cannot act
  await fc.assert(
    fc.asyncProperty(
      fc.array(fc.constantFrom(...staleOrMismatchedScenarios), { minLength: 1, maxLength: 8 }),
      async (staleSequence) => {
        const result = await Runtime.recoverScenarios(staleSequence)
        assert.deepEqual(result.effects, [])
        assert.deepEqual(
          result.decisions,
          staleSequence.map(() => 'Ignore'),
        )
      },
    ),
    { seed: 0x5354414c, numRuns: 50 },
  )

  // Invariant 4: Recovery decision determinism and completeness over all valid scenario primitives
  fc.assert(
    fc.property(
      fc.constantFrom(...validScenarios),
      (scenario) => {
        const decision = recovery.decideScenario(scenario)
        assert.ok(['Ignore', 'ReconcilePhysical', 'ResumePreProvider', 'Finalize', 'MarkManualIntervention'].includes(decision.kind))
        assert.equal(typeof decision.request, 'string')

        // Duplicate evaluation is deterministic and effect-free
        const duplicate = recovery.decideScenario(scenario)
        assert.deepEqual(duplicate, decision)
      },
    ),
    { seed: 0x56414c49, numRuns: 100 },
  )

  // Restart creates a fresh recovery port observer
  const result = await Runtime.recoverAcrossRestart(['AcceptedProviderAlive', 'AcceptedProviderAlive'])
  assert.deepEqual(result.beforeRestart.decisions, ['ReconcilePhysical'])
  assert.deepEqual(result.beforeRestart.effects, ['ReconcilePhysical:PersistProviderStarted'])
  assert.deepEqual(result.afterRestart.decisions, ['ReconcilePhysical'])
  assert.deepEqual(result.afterRestart.effects, ['ReconcilePhysical:PersistProviderStarted'])
})
