import assert from 'node:assert/strict'
import test from 'node:test'

import * as lifecycle from '../../../dist/Execution/Session/ChatExecution/Surface.js'
import * as recovery from '../../../dist/Execution/Session/ChatExecution/RecoveryRuntimeSurface.js'
import * as transaction from '../../../dist/OpenCode/Host/ChatAdmission/TransactionSurface.js'

const evidence = (cut) => ({
  sessionId: `ses-crash-${cut}`,
  physicalUserMessageId: `msg-crash-${cut}`,
  logicalRunId: `run-crash-${cut}`,
  authorityRootUserMessageId: `root-crash-${cut}`,
  identitySeed: {
    participantIdentity: {
      selectedAgent: 'coder',
    },
  },
  providerRun: `provider-crash-${cut}`,
})

const action = (kind, cut, extra = {}) => ({
  kind,
  evidence: {
    ...evidence(cut),
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
    origin: 'HumanRoot',
    effectiveAgent: 'coder',
    requestKind: 'work-main',
    projectionChoice: { kind: 'UseCommittedEpoch' },
  },
  appendOutcome: 'Committed',
  ...extra,
})

const CUTS = [
  ['A', 'before Accepted append', [], 'None', { activeCapacity: 0, providerBinding: 0, hostProjected: false }, 0, 'NoDurableExecution'],
  ['B', 'after Accepted before lease', ['Accepted'], 'Accepted', { activeCapacity: 0, providerBinding: 0, hostProjected: false }, 0, 'ResumePreProvider'],
  ['C', 'after lease before execution binding', ['Accepted'], 'Accepted', { activeCapacity: 1, providerBinding: 0, hostProjected: false }, 0, 'ResumePreProvider'],
  ['D', 'after binding before Host projection', ['Accepted'], 'Accepted', { activeCapacity: 1, providerBinding: 1, hostProjected: false }, 0, 'ResumePreProvider'],
  ['E', 'after Host projection before ProviderStarted', ['Accepted'], 'Accepted', { activeCapacity: 1, providerBinding: 1, hostProjected: true }, 0, 'ResumePreProvider'],
  ['F', 'after ProviderStarted before Terminal', ['Accepted', 'ProviderStarted'], 'ProviderStarted', { activeCapacity: 1, providerBinding: 1, hostProjected: true }, 1, 'Ignore'],
  ['G', 'after Terminal before exact release', ['Accepted', 'ProviderStarted', 'Terminal'], 'Terminal', { activeCapacity: 1, providerBinding: 1, hostProjected: true }, 1, 'ReconcilePhysical'],
  ['H', 'after exact release before Hook return', ['Accepted', 'ProviderStarted', 'Terminal'], 'Terminal', { activeCapacity: 0, providerBinding: 0, hostProjected: true }, 1, 'Ignore'],
  ['I', 'after exact release before fatal propagation', ['Accepted', 'ProviderStarted', 'Terminal'], 'Terminal', { activeCapacity: 0, providerBinding: 0, hostProjected: true }, 1, 'Ignore'],
]

const expectedEffects = (decision) => ({
  NoDurableExecution: [],
  ResumePreProvider: ['ResumePreProvider', 'ResumePreProvider'],
  Ignore: [],
  ReconcilePhysical: [
    'ReconcilePhysical:ReleaseTerminalResource',
    'ReconcilePhysical:ReleaseTerminalResource',
  ],
})[decision]

test('WHAT[CHATEXEC-012] A–I production transaction and lifecycle prefixes drive recovery decisions', async () => {
  const transactionResults = new Map()
  for (const cut of ['A', 'B', 'C', 'D', 'E']) {
    transactionResults.set(cut, await transaction.transactionScenario(evidence(cut), `Crash${cut}`, 'None'))
  }

  const lifecycleResults = new Map()
  lifecycleResults.set('F', await lifecycle.providerLifecycleScenario([
    action('Accept', 'F'),
    action('ProviderStarted', 'F'),
    { kind: 'ProviderWork' },
  ]))
  for (const cut of ['G', 'H', 'I']) {
    lifecycleResults.set(cut, await lifecycle.providerLifecycleScenario([
      action('Accept', cut),
      action('ProviderStarted', cut),
      { kind: 'ProviderWork' },
      action('Terminal', cut, { disposition: 'Completed' }),
    ]))
  }

  for (const restart of ['PluginReload', 'ProcessRestart']) {
    const recovered = await recovery.admissionCrashPointScenarios(CUTS.map(([cut]) => cut), restart, 'NotCommitted', 'Applied')

    for (const [index, [cut, label, facts, phase, local, providerCount, decision]] of CUTS.entries()) {
      const recoveryResult = recovered.scenarios[index]
      assert.equal(recoveryResult.cut, cut, label)
      assert.equal(recoveryResult.restart, restart, label)
      assert.deepEqual(recoveryResult.decisions, [decision, decision], label)
      assert.deepEqual(recoveryResult.effects, expectedEffects(decision), label)
      assert.equal(recoveryResult.commitment, 'NotCommitted', label)

      if (transactionResults.has(cut)) {
        const result = transactionResults.get(cut)
        assert.equal(result.crashed, true, label)
        assert.equal(result.durableLifecycle, phase, label)
        assert.deepEqual(result.admission, local, label)
        assert.equal(result.providerCount, providerCount, label)
      } else {
        const result = lifecycleResults.get(cut)
        assert.equal(result.ok, true, `${label}: ${JSON.stringify(result.error)}`)
        assert.equal(result.projection.phase, phase, label)
        assert.deepEqual(result.appendCounts, {
          accepted: facts.includes('Accepted') ? 1 : 0,
          providerStarted: facts.includes('ProviderStarted') ? 1 : 0,
          terminal: facts.includes('Terminal') ? 1 : 0,
        }, label)
        assert.equal(result.semanticTransitionCount, facts.length, label)
        assert.equal(result.providerWorkCount, providerCount, label)
      }
    }
  }
})
