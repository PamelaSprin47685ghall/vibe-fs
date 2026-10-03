import assert from 'node:assert/strict'
import * as codec from '../../../../dist/Persistence/Journal/FactCodecSurface.js'
import * as recovery from '../../../../dist/OpenCode/Host/LoadRecoverySurface.js'
import * as identity from '../../../../dist/Participant/Persona/Surface.js'
import * as authority from '../../../../dist/Interaction/Authority/RuntimeSurface.js'

export { recovery }

export const fold = (state, fact) => {
  const line = typeof fact === 'string' ? fact : codec.encode(fact)
  const result = recovery.foldCanonical(state, line)
  assert.equal(result.ok, true, result.error)
}

export const link = (state, { parent = 'parent', child = 'child', handle = 'work', agent = 'engineer', byname = 'review-change', ownership = 'DurableParentHandle' } = {}) => fold(state, {
  family: 'Execution', case: 'HandleLinked', payload: {
    ParentSessionId: parent, ChildSessionId: child, Handle: handle,
    TargetAgent: agent, Byname: byname, CanonicalRole: agent, Ownership: ownership,
  },
})

const rootFact = profile => ({
  family: 'Prompt', case: 'AuthorityRootAccepted', payload: {
    SchemaVersion: 2, SessionId: profile.session, LogicalRunId: profile.logicalRun,
    AuthorityRootUserMessageId: profile.authorityRoot, AuthorityKind: profile.authorityKind,
    IdentitySeed: profile.identitySeed,
  },
})

const createRoot = (session, kind, seed) => {
  const result = authority.createAuthorityRoot(value => `load-fixture:${value}`, 'load-fixture-runtime', session, kind, `root-${session}`, seed)
  assert.equal(result.ok, true, result.error)
  return result.value
}

const humanProfile = (session, agent) => {
  const resolved = identity.resolveParticipantIdentityAtRoot(agent)
  assert.equal(resolved.ok, true, resolved.error)
  const value = resolved.identity
  return createRoot(session, 'HumanRoot', {
    kind: 'RootSelection', ownerSession: null, ownerLogicalRun: null, ownerAuthorityRoot: null,
    participantIdentity: {
      participant: value.name, role: value.role, persona: value.persona,
      personaCatalogVersion: value.catalogVersion, origin: value.origin,
    },
  })
}

export const acceptRun = (state, { child = 'child', agent = 'engineer', parent = 'parent', human = false } = {}) => {
  let profile
  if (human) {
    profile = humanProfile(child, agent)
  } else {
    const owner = humanProfile(parent, 'manager')
    fold(state, rootFact(owner))
    const issued = authority.issueInheritedIdentitySeed(agent, owner)
    assert.equal(issued.ok, true, issued.error)
    profile = createRoot(child, 'AgentOwnerRoot', issued.value)
    // The production AgentOwnerRoot chain is Claimed → PhysicalAccepted →
    // AuthorityRootAccepted (dispatch-protocol-002 four-state claim lifecycle;
    // interaction-authority-001 promotes a root only after its physical
    // message's landing is durable). admitWork's exactLanding check reads that
    // landing by the root's physical message id, so the fixture must fold the
    // same two dispatch facts the real writer emits before the root fact.
    // FactCodecSurface's encode table does not cover these dispatch facts, so
    // the canonical line is assembled by hand and the identity seed must carry
    // its durable wire shape (PromptFactCodec identitySeedEncoder):
    // ["InheritedFromOwner", { OwnerSessionId, OwnerLogicalRunId,
    //   OwnerAuthorityRootUserMessageId, ParticipantIdentity }] — not the
    // camelCase JS projection shape.
    const wireSeed = seed =>
      ['InheritedFromOwner', {
        OwnerSessionId: id('SessionId', seed.ownerSession),
        OwnerLogicalRunId: id('LogicalRunId', seed.ownerLogicalRun),
        OwnerAuthorityRootUserMessageId: id('AuthorityRootUserMessageId', seed.ownerAuthorityRoot),
        ParticipantIdentity: {
          InitialTier: 'deep',
          Origin: 'InheritedFromOwner',
          Persona: seed.participantIdentity.persona,
          PersonaCatalogVersion: seed.participantIdentity.personaCatalogVersion,
          Role: seed.participantIdentity.role,
          SelectedAgent: seed.participantIdentity.participant,
        },
      }]
    const promptKey = id('PromptKey', `key-${child}`)
    fold(state, canonical('Prompt', 'PluginPromptClaimed', {
      PromptKey: promptKey,
      SessionId: id('SessionId', child),
      ContinuationKind: 'AgentOwnerRoot',
      LogicalRunId: null,
      AuthorityRootUserMessageId: null,
      IdentitySeed: wireSeed(profile.identitySeed),
      PayloadDigest: `digest-${child}`,
    }))
    fold(state, canonical('Prompt', 'PluginPromptPhysicalAccepted', {
      PromptKey: promptKey,
      SessionId: id('SessionId', child),
      PhysicalUserMessageId: id('PhysicalUserMessageId', profile.authorityRoot),
    }))
  }
  fold(state, rootFact(profile))
  return profile
}

export const terminal = (disposition, profile) => {
  const root = JSON.parse(codec.encode(rootFact(profile)))[1][1][1]
  const accepted = {
    SessionId: root.SessionId, LogicalRunId: root.LogicalRunId,
    AuthorityRootUserMessageId: root.AuthorityRootUserMessageId,
    AuthorityKind: root.AuthorityKind, IdentitySeed: root.IdentitySeed,
    PhysicalUserMessageId: ['PhysicalUserMessageId', 'message-child'],
    Origin: ['AuthorityRoot', 'AgentOwnerRoot'],
  }
  return canonical('ChatExecution', 'Terminal', {
    SchemaVersion: 1,
    Key: { SessionId: root.SessionId, PhysicalUserMessageId: accepted.PhysicalUserMessageId },
    Evidence: ['AfterProviderStart', {
      Accepted: accepted, ProviderRun: ['ProviderRunIdentity', 'provider-child'],
      RequestKind: 'WorkMain', ProjectionChoice: 'UseCommittedEpoch',
    }],
    Disposition: disposition,
  })
}

// Canonical journal JSON is a supported durable wire format, not Fable runtime object layout.
export const canonical = (family, name, payload) => JSON.stringify(['Agent', [family, [name, payload]]])
export const id = (kind, value) => [kind, value]

export const materialized = (request = 'request', blogger = 'blogger') => canonical('Context', 'BloggerRequestMaterialized', {
  RequestId: id('BloggerRequestId', request), MainSessionId: id('SessionId', 'parent'),
  BloggerSessionId: id('SessionId', blogger), RequestKind: 'main',
  ContextRef: id('BlobRef', 'blobs/context'), ContextDigest: id('BlobDigest', 'digest-context'),
  ObservedPrefixEpochId: id('PrefixEpochId', 0), PreviousIngestedThroughSequence: '699',
  NextIngestedThroughSequence: '706', FrameEpochId: id('FrameEpochId', 0),
  SelectedFrameDigests: [], PromptKey: null,
})

export const abandoned = (request = 'request', blogger = 'blogger') => canonical('Context', 'BloggerRequestAbandoned', {
  RequestId: id('BloggerRequestId', request), MainSessionId: id('SessionId', 'parent'),
  BloggerSessionId: id('SessionId', blogger), Reason: 'stale-open-at-load',
})

export const admittedFission = () => canonical('Fission', 'FissionAdmitted', {
  GroupId: 'group', OwnerSessionId: id('SessionId', 'owner'), ParentSessionId: null,
  OriginToolCallId: id('ToolCallId', 'fission-call'), LaneCount: 2,
  LaneSessions: [id('SessionId', 'lane-0'), id('SessionId', 'lane-1')],
  LanePrompts: ['left', 'right'], OwnerWorkRecordRef: id('BlobRef', 'blobs/work'),
  OwnerWorkRecordDigest: id('BlobDigest', 'digest-work'), PreFissionCompletionIds: [],
})
