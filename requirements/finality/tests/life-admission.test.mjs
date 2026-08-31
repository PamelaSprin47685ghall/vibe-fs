// requirements/finality/tests/life-admission.test.mjs
//
// FINALITY-022 / INTERACTION-AUTHORITY-009: Life admission decisions.
// PR 6 exemplar: the test speaks lifecycle vocabulary; FinalitySurface owns
// the profile construction and the F# boundary.

import assert from 'node:assert/strict'
import test from 'node:test'

const finality = await import('../../../dist/Mission/Manager/FinalitySurface.js')

const project = (events) => {
  let world = finality.emptyWorld()
  for (const event of events) {
    const result = finality.applyEvent(world, event)
    assert.equal(result.ok, true, JSON.stringify(result.error))
    world = result.world
  }
  return world
}

const emptyWorld = project([])

const ownerSession = 'ses-finality-owner'
const ownerLogicalRun = 'run-finality-owner'
const ownerAuthorityRoot = 'msg-finality-owner-root'

const participantIdentity = (origin) => ({
  selectedAgent: 'fast-manager',
  peerAgent: 'deep-manager',
  canonicalRole: 'manager',
  selectedTier: 'fast',
  persona: 'Coordinator',
  personaCatalogVersion: 1,
  origin,
})

const ownerAuthorityAccepted = {
  kind: 'authority-root-accepted',
  sessionId: ownerSession,
  logicalRunId: ownerLogicalRun,
  authorityRootUserMessageId: ownerAuthorityRoot,
  authorityKind: 'HumanRoot',
  identitySeed: {
    kind: 'RootSelection',
    participantIdentity: participantIdentity('ResolvedAtRoot'),
  },
}

const inheritedOwnerIdentitySeed = {
  kind: 'InheritedFromOwner',
  ownerSession,
  ownerLogicalRun,
  ownerAuthorityRoot,
  participantIdentity: participantIdentity('InheritedFromOwner'),
}

const ownerWorld = project([ownerAuthorityAccepted])

const agentOwnerEnding = (opening) =>
  finality.endingAdmission(ownerWorld, 'agent-owner-root', 'root-1', 'fast-manager', 'deep-manager', 'fast', {
    ...opening,
    identitySeed: inheritedOwnerIdentitySeed,
  })

test('WHAT[FINALITY-022] unknown authority kind and tier fail closed', () => {
  const unknownKind = finality.endingAdmission(emptyWorld, 'forged-root', 'root-1', 'fast-manager', 'deep-manager', 'fast', { assignmentText: 'work' })
  assert.equal(unknownKind.ok, false)
  assert.match(unknownKind.error, /unknown authority kind/)

  const unknownTier = finality.endingAdmission(emptyWorld, 'agent-owner-root', 'root-1', 'fast-manager', 'deep-manager', 'forged', { assignmentText: 'work' })
  assert.equal(unknownTier.ok, false)
  assert.match(unknownTier.error, /unknown tier/)
  assert.equal(finality.tryHumanRootOpening(emptyWorld, 'forged-root', 'root-1', 'root-1'), false)
})

test('WHAT[FINALITY-022] AgentOwner migration is admitted only before any Life history', () => {
  const opening = { assignmentText: 'work' }

  const first = agentOwnerEnding(opening)
  assert.deepEqual(first, { kind: 'initial-agent-owner-migration' }, 'first AgentOwner ending may materialize one migration Life')

  // A Life that was completed and archived: CurrentLife=None after completion
  // is terminal closure, never permission to rematerialize XTrace.
  const closed = project([
    {
      kind: 'life-opened',
      sessionId: 'ses-admission',
      lifeId: 'life-1',
      openingUserMessageId: 'msg-open',
      openingTextRef: 'blob-1',
      openingTextDigest: 'digest-1',
      openingCursorSequence: 1,
    },
    {
      kind: 'finality-requested',
      sessionId: 'ses-admission',
      lifeId: 'life-1',
      requestId: 'req-1',
      gitTreeHash: 'tree-1',
      lastWordsRef: 'blob-1',
      lastWordsDigest: 'digest-1',
      providerRun: 'run-1',
      toolCallId: 'call-1',
    },
    {
      kind: 'finality-reviewer-enlisted',
      sessionId: 'ses-admission',
      lifeId: 'life-1',
      requestId: 'req-1',
      reviewerSessionId: 'ses-reviewer',
      reviewerOrdinal: 1,
      barrierId: 'bar-1',
      gitTreeHash: 'tree-1',
      isNewReviewer: true,
    },
    {
      kind: 'finality-blessed',
      sessionId: 'ses-admission',
      lifeId: 'life-1',
      requestId: 'req-1',
      gitTreeHash: 'tree-1',
      workRecordBundleRef: 'blob-1',
      workRecordBundleDigest: 'digest-1',
    },
    {
      kind: 'life-completed',
      sessionId: 'ses-admission',
      lifeId: 'life-1',
      requestId: 'req-1',
      terminalRef: 'blob-1',
      terminalDigest: 'digest-1',
    },
    ownerAuthorityAccepted,
  ])
  assert.equal(finality.archivedLivesView(closed).length, 1)

  const afterCompletion = finality.endingAdmission(
    closed,
    'agent-owner-root',
    'root-1',
    'fast-manager',
    'deep-manager',
    'fast',
    { ...opening, identitySeed: inheritedOwnerIdentitySeed },
  )
  assert.deepEqual(
    afterCompletion,
    { kind: 'no-life' },
    'CurrentLife=None after completion is terminal closure, never permission to rematerialize XTrace',
  )
})

test('WHAT[FINALITY-022] HumanRoot opening requires the exact authority root message id', () => {
  const exact = finality.tryHumanRootOpening(emptyWorld, 'human-root', 'root-1', 'root-1')
  assert.equal(exact, true, 'the active authority-root message itself must open the Life')

  const laterUser = finality.tryHumanRootOpening(emptyWorld, 'human-root', 'root-1', 'later-user-shaped-message')
  assert.equal(
    laterUser,
    false,
    'session-level HumanRoot authority must not turn another user-shaped message into a root',
  )
})
