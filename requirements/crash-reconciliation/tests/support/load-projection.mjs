import assert from 'node:assert/strict'
import * as codec from '../../../../dist/Persistence/Journal/FactCodecSurface.js'
import * as recovery from '../../../../dist/OpenCode/Host/LoadRecoverySurface.js'

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

const rootFact = ({ child = 'child', agent = 'engineer', parent = 'parent', human = false } = {}) => ({
  family: 'Prompt', case: 'AuthorityRootAccepted', payload: {
    SchemaVersion: 2, SessionId: child, LogicalRunId: `run-${child}`,
    AuthorityRootUserMessageId: `root-${child}`, AuthorityKind: human ? 'HumanRoot' : 'AgentOwnerRoot',
    IdentitySeed: {
      kind: human ? 'RootSelection' : 'InheritedFromOwner',
      ownerSession: human ? null : parent,
      ownerLogicalRun: human ? null : `run-${parent}`,
      ownerAuthorityRoot: human ? null : `root-${parent}`,
      participantIdentity: {
        participant: agent, role: agent, persona: agent === 'manager' ? 'Lead' : agent === 'devops' ? 'DevOps' : 'Engineer',
        personaCatalogVersion: 1, origin: human ? 'ResolvedAtRoot' : 'InheritedFromOwner',
      },
    },
  },
})

export const acceptRun = (state, options) => fold(state, rootFact(options))

export const terminal = disposition => {
  const root = JSON.parse(codec.encode(rootFact()))[1][1][1]
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
