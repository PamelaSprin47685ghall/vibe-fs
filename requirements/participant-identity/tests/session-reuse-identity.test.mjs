import assert from 'node:assert/strict'
import test from 'node:test'

import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as temporal from '../../../dist/Verification/TemporalSurface.js'

const hash = (value) => `H(${value})`
const session = 'ses-reusable-physical-container'

const rootSeed = ({ selectedAgent, peerAgent, canonicalRole, selectedTier, persona }) => ({
  kind: 'RootSelection',
  ownerSession: null,
  ownerLogicalRun: null,
  ownerAuthorityRoot: null,
  participantIdentity: {
    selectedAgent,
    peerAgent,
    canonicalRole,
    selectedTier,
    persona,
    personaCatalogVersion: 1,
    origin: 'ResolvedAtRoot',
  },
})

const managerSeed = rootSeed({
  selectedAgent: 'fast-manager',
  peerAgent: 'deep-manager',
  canonicalRole: 'manager',
  selectedTier: 'fast',
  persona: 'Coordinator',
})

const reviewerSeed = rootSeed({
  selectedAgent: 'deep-reviewer',
  peerAgent: 'fast-reviewer',
  canonicalRole: 'reviewer',
  selectedTier: 'deep',
  persona: 'Auditor',
})

const createRoot = (physical, seed) => {
  const result = authority.createAuthorityRoot(
    hash,
    'runtime-session-reuse',
    session,
    'HumanRoot',
    physical,
    seed,
  )
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.value
}

const acceptedFact = (profile) => ({
  family: 'Prompt',
  case: 'AuthorityRootAccepted',
  payload: {
    SchemaVersion: 2,
    SessionId: profile.session,
    LogicalRunId: profile.logicalRun,
    AuthorityRootUserMessageId: profile.authorityRoot,
    AuthorityKind: profile.authorityKind,
    IdentitySeed: {
      Kind: profile.identitySeed.kind,
      OwnerSessionId: profile.identitySeed.ownerSession,
      OwnerLogicalRunId: profile.identitySeed.ownerLogicalRun,
      OwnerAuthorityRootUserMessageId: profile.identitySeed.ownerAuthorityRoot,
      ParticipantIdentity: {
        SelectedAgent: profile.participantIdentity.selectedAgent,
        PeerAgent: profile.participantIdentity.peerAgent,
        Role: profile.participantIdentity.canonicalRole,
        InitialTier: profile.participantIdentity.selectedTier,
        Persona: profile.participantIdentity.persona,
        PersonaCatalogVersion: profile.participantIdentity.personaCatalogVersion,
        Origin: profile.participantIdentity.origin,
      },
    },
  },
})

const current = (projection) => projection.sessions[session].activeLogicalRun

test('WHAT[PID-011] reuses SessionId with a fresh closed-run identity', () => {
  const first = createRoot('msg-run-a', managerSeed)
  const firstSnapshot = structuredClone(first)
  const second = createRoot('msg-run-b', reviewerSeed)

  assert.notEqual(second.logicalRun, first.logicalRun)
  assert.notEqual(second.authorityRoot, first.authorityRoot)
  assert.notDeepEqual(second.participantIdentity, first.participantIdentity)

  const activeFirst = authority.registerAuthority(first, authority.empty)
  const prematureSecond = authority.registerAuthority(second, activeFirst)
  assert.equal(prematureSecond.ok, false)
  assert.equal(prematureSecond.error.kind, 'ActiveRunIdentityConflict')
  assert.deepEqual(prematureSecond.error.active, first)
  assert.deepEqual(prematureSecond.error.requested, second)

  for (const [logicalRun, authorityRoot] of [
    [second.logicalRun, first.authorityRoot],
    [first.logicalRun, second.authorityRoot],
  ]) {
    const wrongClose = authority.closeAuthority(logicalRun, authorityRoot, activeFirst)
    assert.equal(wrongClose.ok, false)
    assert.match(wrongClose.error, /logical-run close mismatch/)
  }

  const oldInheritedSeed = authority.issueInheritedIdentitySeed('fast-reviewer', first)
  assert.equal(oldInheritedSeed.ok, true, oldInheritedSeed.ok ? '' : oldInheritedSeed.error)

  const closedFirst = authority.closeAuthority(
    first.logicalRun,
    first.authorityRoot,
    activeFirst,
  )
  assert.equal(closedFirst.ok, true, closedFirst.ok ? '' : closedFirst.error)
  assert.equal(closedFirst.value.activeLogicalRun, null)
  assert.deepEqual(closedFirst.value.lastAuthorityProfile, first)

  const activeSecond = authority.registerAuthority(second, closedFirst.value)
  assert.deepEqual(activeSecond.activeLogicalRun, second)
  assert.deepEqual(activeSecond.lastAuthorityProfile, second)
  assert.deepEqual(first, firstSnapshot)

  const inheritedAgainstSecond = authority.validateInheritedIdentitySeed(
    second,
    oldInheritedSeed.value,
  )
  assert.equal(inheritedAgainstSecond.ok, false)
  assert.equal(inheritedAgainstSecond.error.kind, 'OwnerLogicalRunIdMismatch')
  assert.equal(inheritedAgainstSecond.error.expected, second.logicalRun)
  assert.equal(inheritedAgainstSecond.error.actual, first.logicalRun)

  const scenario = temporal.sessionReuseIdentityScenario(
    acceptedFact(first),
    acceptedFact(second),
  )
  assert.equal(scenario.preCloseSecond.ok, false)
  assert.match(
    scenario.preCloseSecond.error.Reason,
    /active logical run must close before replacement/,
  )
  assert.deepEqual(current(scenario.afterFirst), {
    session,
    logicalRun: first.logicalRun,
    authorityRoot: first.authorityRoot,
    authorityKind: 'HumanRoot',
    identitySeed: acceptedFact(first).payload.IdentitySeed,
    participantIdentity: acceptedFact(first).payload.IdentitySeed.ParticipantIdentity,
  })
  assert.equal(scenario.afterLife.sessions[session].activeLogicalRun, null)
  assert.deepEqual(
    scenario.afterLife.sessions[session].lastAuthorityProfile,
    current(scenario.afterFirst),
  )

  const onlineCurrent = current(scenario.online)
  const replayedCurrent = current(scenario.replayed)
  assert.deepEqual(onlineCurrent, {
    session,
    logicalRun: second.logicalRun,
    authorityRoot: second.authorityRoot,
    authorityKind: 'HumanRoot',
    identitySeed: acceptedFact(second).payload.IdentitySeed,
    participantIdentity: acceptedFact(second).payload.IdentitySeed.ParticipantIdentity,
  })
  assert.deepEqual(replayedCurrent, onlineCurrent)
  assert.notEqual(replayedCurrent.logicalRun, first.logicalRun)
  assert.notDeepEqual(
    replayedCurrent.participantIdentity,
    acceptedFact(first).payload.IdentitySeed.ParticipantIdentity,
  )
})
