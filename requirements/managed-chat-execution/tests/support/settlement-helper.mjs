import * as transaction from '../../../../dist/OpenCode/Host/ChatAdmission/TransactionSurface.js'
import * as executionStatus from '../../../../dist/Execution/Session/ChatExecution/StatusSurface.js'

const defaultEvidence = (suffix = 'test', overrides = {}) => ({
  sessionId: `ses-pre-provider-${suffix}`,
  physicalUserMessageId: `msg-pre-provider-${suffix}`,
  logicalRunId: `run-pre-provider-${suffix}`,
  authorityRootUserMessageId: `root-pre-provider-${suffix}`,
  authorityKind: 'HumanRoot',
  identitySeed: {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: {
      selectedAgent: 'coder',
      canonicalRole: 'coder',
      selectedTier: 'deep',
      persona: 'Coder',
      personaCatalogVersion: 1,
      origin: 'ResolvedAtRoot',
    },
  },
  providerRun: `provider-pre-provider-${suffix}`,
  origin: 'HumanRoot',
  requestKind: 'work-main',
  projectionChoice: { kind: 'UseCommittedEpoch' },
  ...overrides,
})

export const testPreCommitFailureRelease = async () => {
  const expectations = ['LeaseTarget', 'BindExecution', 'ProjectHost', 'CommitLease']
  for (const failurePoint of expectations) {
    const result = await transaction.transactionScenario(defaultEvidence('pre-commit'), failurePoint, 'None')
    if (result.releaseCount !== 1) return { releases: result.releaseCount }
  }
  return { releases: 1 }
}

export const testReleaseBoundaryFailure = async () => {
  const result = await transaction.transactionScenario(defaultEvidence('release-boundary'), 'ReleaseBeforeProvider', 'None')
  return { releases: result.releaseCount }
}

export const testPreProviderSettlement = async () => {
  const cases = [
    ['IdentityConflict', 'Rejected'],
    ['ProjectionError', 'Failed'],
    ['ExecutionBindingError', 'Failed'],
    ['FatalMembraneInput', 'Failed'],
    ['Supersession', 'Cancelled'],
    ['PluginReplay', 'Rejected'],
  ]
  for (const [failure, disposition] of cases) {
    const result = await transaction.preProviderSettlementScenario(defaultEvidence(failure), failure, 'Exact')
    const projected = executionStatus.queryFacts(
      result.facts,
      result.key.sessionId,
      result.key.physicalUserMessageId,
    )
    if (!projected.ok || projected.status.disposition !== disposition) {
      return { settled: false }
    }
  }
  return { settled: true }
}

export const testRejectAgent028Membrane = async () => {
  const result = await transaction.preProviderSettlementScenario(
    defaultEvidence('raw-agent', { effectiveAgent: 'AGENT-028' }),
    'FatalMembraneInput',
    'Exact',
  )
  const projected = executionStatus.queryFacts(
    result.facts,
    result.key.sessionId,
    result.key.physicalUserMessageId,
  )
  return {
    rejected: projected.ok && projected.status.disposition === 'Rejected' && result.providerEffectCount === 0,
  }
}

export const testMissingReleaseDetection = async () => {
  const settled = await transaction.preProviderSettlementScenario(
    defaultEvidence('release-control'),
    'ProjectionError',
    'Exact',
  )
  const mutated = await transaction.preProviderSettlementScenario(
    defaultEvidence('release-mutation'),
    'ProjectionError',
    'SkipExactRelease',
  )
  return {
    detected: settled.admission.activeCapacity === 0 && mutated.admission.activeCapacity === 1,
  }
}
