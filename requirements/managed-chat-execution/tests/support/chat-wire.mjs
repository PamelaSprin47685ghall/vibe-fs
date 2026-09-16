const tagged = (name, value) => [name, value]
const keyWire = (sessionId, physicalUserMessageId) => ({
  SessionId: tagged('SessionId', sessionId),
  PhysicalUserMessageId: tagged('PhysicalUserMessageId', physicalUserMessageId),
})
const factWire = (factCase, payload) => JSON.stringify(['Agent', ['ChatExecution', [factCase, payload]]])

const acceptedStore = new Map()

export const clearAcceptedStore = () => {
  acceptedStore.clear()
}

export const acceptManagedChat = (logicalRunId, authorityRootUserMessageId, authorityKind, identitySeed, key, requestKind = 'work-main') => {
  const sessionId = key.sessionId
  const physicalUserMessageId = key.physicalUserMessageId
  const participantInput = identitySeed?.participantIdentity ?? {}
  const seedPayload = identitySeed?.kind === 'RootSelection'
    ? [
        'RootSelection',
        {
          InitialTier: 'deep',
          Origin: participantInput.origin ?? 'ResolvedAtRoot',
          Persona: participantInput.persona ?? 'Coder',
          PersonaCatalogVersion: participantInput.personaCatalogVersion ?? 1,
          Role: participantInput.role ?? 'coder',
          SelectedAgent: participantInput.participant ?? participantInput.selectedAgent ?? 'coder',
        },
      ]
    : [
        'InheritedFromOwner',
        {
          OwnerSession: tagged('SessionId', identitySeed?.ownerSession ?? 'ses-owner'),
          OwnerLogicalRun: tagged('LogicalRunId', identitySeed?.ownerLogicalRun ?? 'run-owner'),
          OwnerAuthorityRoot: tagged('AuthorityRootUserMessageId', identitySeed?.ownerAuthorityRoot ?? 'root-owner'),
          Participant: {
            Origin: participantInput.origin ?? 'InheritedFromOwner',
            Persona: participantInput.persona ?? 'Coder',
            PersonaCatalogVersion: participantInput.personaCatalogVersion ?? 1,
            Role: participantInput.role ?? 'coder',
            SelectedAgent: participantInput.participant ?? participantInput.selectedAgent ?? 'coder',
          },
        },
      ]

  const evidence = {
    SessionId: tagged('SessionId', sessionId),
    LogicalRunId: tagged('LogicalRunId', logicalRunId),
    AuthorityRootUserMessageId: tagged('AuthorityRootUserMessageId', authorityRootUserMessageId),
    AuthorityKind: authorityKind,
    IdentitySeed: seedPayload,
    PhysicalUserMessageId: tagged('PhysicalUserMessageId', physicalUserMessageId),
    Origin: ['AuthorityRoot', authorityKind],
  }

  acceptedStore.set(`${sessionId}:${physicalUserMessageId}`, evidence)

  return factWire('Accepted', {
    Evidence: evidence,
    Key: keyWire(sessionId, physicalUserMessageId),
    SchemaVersion: 1,
  })
}

export const providerStarted = (key, providerRun = `provider-${key.physicalUserMessageId}`, projectionChoice = 'use-committed-epoch', requestKind = 'work-main') => {
  const sessionId = key.sessionId
  const physicalUserMessageId = key.physicalUserMessageId
  const acceptedEvidence = acceptedStore.get(`${sessionId}:${physicalUserMessageId}`) ?? {
    SessionId: tagged('SessionId', sessionId),
    LogicalRunId: tagged('LogicalRunId', `run-${physicalUserMessageId}`),
    AuthorityRootUserMessageId: tagged('AuthorityRootUserMessageId', `root-${physicalUserMessageId}`),
    AuthorityKind: 'HumanRoot',
    IdentitySeed: [
      'RootSelection',
      {
        InitialTier: 'deep',
        Origin: 'ResolvedAtRoot',
        Persona: 'Coder',
        PersonaCatalogVersion: 1,
        Role: 'coder',
        SelectedAgent: 'coder',
      },
    ],
    PhysicalUserMessageId: tagged('PhysicalUserMessageId', physicalUserMessageId),
    Origin: ['AuthorityRoot', 'HumanRoot'],
  }

  const reqKind = requestKind === 'work-main' ? 'WorkMain' : requestKind

  return factWire('ProviderStarted', {
    Evidence: {
      Accepted: acceptedEvidence,
      ProviderRun: tagged('ProviderRunIdentity', providerRun),
      RequestKind: reqKind,
      ProjectionChoice: 'UseCommittedEpoch',
    },
    Key: keyWire(sessionId, physicalUserMessageId),
    SchemaVersion: 1,
  })
}

export const terminal = (key, providerRun, disposition = 'Completed') => {
  const sessionId = key.sessionId
  const physicalUserMessageId = key.physicalUserMessageId
  const acceptedEvidence = acceptedStore.get(`${sessionId}:${physicalUserMessageId}`) ?? {
    SessionId: tagged('SessionId', sessionId),
    LogicalRunId: tagged('LogicalRunId', `run-${physicalUserMessageId}`),
    AuthorityRootUserMessageId: tagged('AuthorityRootUserMessageId', `root-${physicalUserMessageId}`),
    AuthorityKind: 'HumanRoot',
    IdentitySeed: [
      'RootSelection',
      {
        InitialTier: 'deep',
        Origin: 'ResolvedAtRoot',
        Persona: 'Coder',
        PersonaCatalogVersion: 1,
        Role: 'coder',
        SelectedAgent: 'coder',
      },
    ],
    PhysicalUserMessageId: tagged('PhysicalUserMessageId', physicalUserMessageId),
    Origin: ['AuthorityRoot', 'HumanRoot'],
  }

  const startedEvidence = {
    Accepted: acceptedEvidence,
    ProviderRun: tagged('ProviderRunIdentity', providerRun),
    RequestKind: 'WorkMain',
    ProjectionChoice: 'UseCommittedEpoch',
  }

  return factWire('Terminal', {
    Disposition: disposition,
    Evidence: ['AfterProviderStart', startedEvidence],
    Key: keyWire(sessionId, physicalUserMessageId),
    SchemaVersion: 1,
  })
}

export const terminalPreProvider = (key, disposition) => {
  const sessionId = key.sessionId
  const physicalUserMessageId = key.physicalUserMessageId
  const acceptedEvidence = acceptedStore.get(`${sessionId}:${physicalUserMessageId}`) ?? {
    SessionId: tagged('SessionId', sessionId),
    LogicalRunId: tagged('LogicalRunId', `run-${physicalUserMessageId}`),
    AuthorityRootUserMessageId: tagged('AuthorityRootUserMessageId', `root-${physicalUserMessageId}`),
    AuthorityKind: 'HumanRoot',
    IdentitySeed: [
      'RootSelection',
      {
        InitialTier: 'deep',
        Origin: 'ResolvedAtRoot',
        Persona: 'Coder',
        PersonaCatalogVersion: 1,
        Role: 'coder',
        SelectedAgent: 'coder',
      },
    ],
    PhysicalUserMessageId: tagged('PhysicalUserMessageId', physicalUserMessageId),
    Origin: ['AuthorityRoot', 'HumanRoot'],
  }

  return factWire('Terminal', {
    Disposition: disposition,
    Evidence: ['PreProvider', acceptedEvidence],
    Key: keyWire(sessionId, physicalUserMessageId),
    SchemaVersion: 1,
  })
}
