import * as admission from '../../../../dist/OpenCode/Host/ChatAdmission/TransactionSurface.js'

const defaultEvidence = {
  sessionId: 'ses-transaction',
  physicalUserMessageId: 'msg-transaction',
  logicalRunId: 'run-transaction',
  authorityRootUserMessageId: 'root-transaction',
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
  providerRun: 'provider-transaction',
  origin: 'HumanRoot',
  requestKind: 'work-main',
  projectionChoice: { kind: 'UseCommittedEpoch' },
}

export const runManagedAdmissionScenario = async (scenario) => {
  switch (scenario) {
    case 'happy-path': {
      const res = await admission.transactionScenario(defaultEvidence, 'None', 'None')
      return { outcome: res.outcome, ...res }
    }
    case 'append-failure': {
      const res = await admission.transactionScenario(defaultEvidence, 'AcceptNotAttempted', 'None')
      return {
        outcome: res.ok ? 'Settled' : 'Refused',
        acquiredCapacity: res.acquireCount > 0,
        ...res,
      }
    }
    case 'acquire-failure': {
      const res = await admission.transactionScenario(defaultEvidence, 'AcquireLease', 'None')
      return {
        outcome: res.ok ? 'Settled' : 'Refused',
        boundProvider: res.bindCount > 0,
        ...res,
      }
    }
    case 'superseded': {
      const res = await admission.transactionScenario(defaultEvidence, 'AcquireSuperseded', 'None')
      return {
        outcome: res.outcome === 'Superseded' ? 'ShortCircuit' : res.outcome,
        ...res,
      }
    }
    case 'already-started-replay': {
      const res = await admission.transactionScenario(defaultEvidence, 'None', 'ProviderStarted')
      return {
        duplicateEffects: res.acquireCount + res.bindCount + res.hostCount + res.providerCount,
        ...res,
      }
    }
    case 'uncertain-persistence': {
      const res = await admission.transactionScenario(defaultEvidence, 'AcceptCommitUnknown', 'None')
      return {
        acquiredCapacity: res.acquireCount > 0,
        ...res,
      }
    }
    case 'accepted-replay': {
      const res = await admission.transactionScenario(defaultEvidence, 'None', 'Accepted')
      return {
        appends: res.appendCount,
        ...res,
      }
    }
    case 'terminal-replay': {
      const res = await admission.transactionScenario(defaultEvidence, 'None', 'Terminal')
      return {
        duplicateEffects: res.acceptCount + res.acquireCount + res.bindCount + res.hostCount + res.providerCount,
        ...res,
      }
    }
    default:
      throw new Error(`unknown scenario ${scenario}`)
  }
}
