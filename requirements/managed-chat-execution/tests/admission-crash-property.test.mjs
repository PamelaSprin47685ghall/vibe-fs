import assert from 'node:assert/strict'
import test from 'node:test'

import * as lifecycle from '../../../dist/Execution/Session/ChatExecution/Surface.js'
import * as recovery from '../../../dist/Execution/Session/ChatExecution/RecoveryRuntimeSurface.js'
import * as transaction from '../../../dist/OpenCode/Host/ChatAdmission/TransactionSurface.js'

const CUTS = [...'ABCDEFGHI']
const rotations = (values) => values.map((_, index) => [...values.slice(index), ...values.slice(0, index)])
const generatedOrders = (values) => [
  values,
  [...values].reverse(),
  ...rotations(values),
  values.flatMap((value) => [value, value]),
]

const canonical = (scenarios) => Object.fromEntries(scenarios.map((scenario) => [scenario.cut, {
  decisions: scenario.decisions,
  effects: scenario.effects,
  commitment: scenario.commitment,
  capacityOutcome: scenario.capacityOutcome,
}]))

const evidence = {
  sessionId: 'ses-crash-property',
  physicalUserMessageId: 'msg-crash-property',
  logicalRunId: 'run-crash-property',
  authorityRootUserMessageId: 'root-crash-property',
  authorityKind: 'HumanRoot',
  identitySeed: {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: {
      selectedAgent: 'fast-coder',
      peerAgent: 'deep-coder',
      canonicalRole: 'coder',
      selectedTier: 'fast',
      persona: 'Coder',
      personaCatalogVersion: 1,
      origin: 'ResolvedAtRoot',
    },
  },
  providerRun: 'provider-crash-property',
  origin: 'HumanRoot',
  effectiveAgent: 'fast-coder',
  requestKind: 'work-main',
  projectionChoice: { kind: 'UseCommittedEpoch' },
}

const action = (kind, extra = {}) => ({ kind, evidence, appendOutcome: 'Committed', ...extra })

test('WHAT[CHATEXEC-012] duplicate causal events and restart permutations equal canonical production replay or fail closed', async () => {
  const baseline = canonical((await recovery.admissionCrashPointScenarios(CUTS, 'PluginReload', 'NotCommitted', 'Applied')).scenarios)

  for (const restart of ['PluginReload', 'ProcessRestart']) {
    for (const order of generatedOrders(CUTS)) {
      const result = await recovery.admissionCrashPointScenarios(order, restart, 'NotCommitted', 'Applied')
      for (const scenario of result.scenarios) {
        assert.deepEqual({
          decisions: scenario.decisions,
          effects: scenario.effects,
          commitment: scenario.commitment,
          capacityOutcome: scenario.capacityOutcome,
        }, baseline[scenario.cut], `${restart}:${order.join('')}:${scenario.cut}`)
        assert.ok(scenario.effects.length <= 1, `${scenario.cut} repeated an owner effect`)
      }
    }
  }

  const duplicateReplay = await lifecycle.providerLifecycleScenario([
    action('Accept'),
    action('Accept'),
    action('ProviderStarted'),
    action('ProviderStarted'),
    action('Terminal', { disposition: 'Completed' }),
    action('Terminal', { disposition: 'Completed' }),
  ])
  const canonicalReplay = await lifecycle.providerLifecycleScenario([
    action('Accept'),
    action('ProviderStarted'),
    action('Terminal', { disposition: 'Completed' }),
  ])
  assert.equal(duplicateReplay.ok, true, JSON.stringify(duplicateReplay.error))
  assert.equal(canonicalReplay.ok, true, JSON.stringify(canonicalReplay.error))
  assert.deepEqual(duplicateReplay.projection, canonicalReplay.projection)
  assert.equal(duplicateReplay.semanticTransitionCount, canonicalReplay.semanticTransitionCount)
  assert.deepEqual(duplicateReplay.appendCounts, canonicalReplay.appendCounts)

  const unknown = await recovery.admissionCrashPointScenarios(['B', 'C', 'D', 'E', 'F'], 'ProcessRestart', 'Unknown', 'Applied')
  for (const scenario of unknown.scenarios) {
    assert.deepEqual(scenario.decisions, ['MarkManualIntervention', 'MarkManualIntervention'])
    assert.deepEqual(scenario.effects, ['MarkManualIntervention:PersistenceOutcomeUnknown'])
  }

  for (const capacityOutcome of ['Conflict', 'StaleFence']) {
    const rejected = await recovery.admissionCrashPointScenarios(['G', 'H', 'I'], 'PluginReload', 'NotCommitted', capacityOutcome)
    for (const scenario of rejected.scenarios) {
      assert.deepEqual(scenario.decisions, ['MarkManualIntervention', 'MarkManualIntervention'])
      assert.deepEqual(scenario.effects, ['MarkManualIntervention:PhysicalOutcomeUnknown'])
    }
  }

  const staleFence = await transaction.transactionScenario(evidence, 'CommitLease', 'None')
  assert.equal(staleFence.ok, false)
  assert.equal(staleFence.error.kind, 'LeaseCommitFailed')
  assert.equal(staleFence.releaseCount, 1)
  assert.equal(staleFence.providerCount, 0)

})
