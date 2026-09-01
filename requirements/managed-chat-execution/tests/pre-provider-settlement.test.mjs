import assert from 'node:assert/strict'
import test from 'node:test'

import * as transaction from '../../../dist/OpenCode/Host/ChatAdmission/TransactionSurface.js'
import * as executionStatus from '../../../dist/Execution/Session/ChatExecution/StatusSurface.js'

const evidence = (suffix, overrides = {}) => ({
  sessionId: `ses-pre-provider-${suffix}`,
  physicalUserMessageId: `msg-pre-provider-${suffix}`,
  logicalRunId: `run-pre-provider-${suffix}`,
  authorityRootUserMessageId: `root-pre-provider-${suffix}`,
  providerRun: `provider-pre-provider-${suffix}`,
  identitySeed: {
    participantIdentity: {
      selectedAgent: 'fast-coder',
    },
  },
  ...overrides,
})

const cases = [
  ['IdentityConflict', 'Rejected'],
  ['ProjectionError', 'Failed'],
  ['ExecutionBindingError', 'Failed'],
  ['FatalMembraneInput', 'Failed'],
  ['Supersession', 'Cancelled'],
  ['PluginReplay', 'Rejected'],
]

test('WHAT[CHATEXEC-007] each typed pre-provider failure settles the exact accepted execution', async () => {
  for (const [failure, disposition] of cases) {
    const result = await transaction.preProviderSettlementScenario(evidence(failure), failure, 'Exact')

    const projected = executionStatus.queryFacts(
      result.facts,
      result.key.sessionId,
      result.key.physicalUserMessageId,
    )

    assert.equal(projected.ok, true)
    assert.deepEqual(projected.status, {
      accepted: true,
      providerStarted: false,
      terminal: true,
      disposition,
    })
    assert.equal(result.acceptedFactCount, 1)
    assert.equal(result.providerEffectCount, 0)
    assert.deepEqual(result.admission, {
      activeCapacity: 0,
      providerBinding: 0,
    })
    assert.equal(result.failure.kind, failure)
    assert.ok(['Recoverable', 'Permanent'].includes(result.failure.classification))
  }
})

test('WHAT[CHATEXEC-007] rejects raw AGENT-028 before it can enter a legal managed flow', async () => {
  const result = await transaction.preProviderSettlementScenario(
    evidence('raw-agent', { effectiveAgent: 'AGENT-028' }),
    'FatalMembraneInput',
    'Exact',
  )
  const projected = executionStatus.queryFacts(
    result.facts,
    result.key.sessionId,
    result.key.physicalUserMessageId,
  )

  assert.equal(projected.ok, true)
  assert.equal(projected.status.disposition, 'Rejected')
  assert.equal(result.admission.activeCapacity, 0)
  assert.equal(result.admission.providerBinding, 0)
  assert.equal(result.providerEffectCount, 0)
})

test('WHAT[CHATEXEC-007] detects missing exact pre-provider release', async () => {
  const settled = await transaction.preProviderSettlementScenario(
    evidence('release-control'),
    'ProjectionError',
    'Exact',
  )
  const mutated = await transaction.preProviderSettlementScenario(
    evidence('release-mutation'),
    'ProjectionError',
    'SkipExactRelease',
  )

  assert.equal(settled.admission.activeCapacity, 0)
  assert.equal(mutated.admission.activeCapacity, 1)
  assert.notDeepEqual(mutated.admission, settled.admission)
})
