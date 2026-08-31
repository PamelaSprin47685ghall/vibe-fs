import assert from 'node:assert/strict'
import test from 'node:test'

import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as persona from '../../../dist/Participant/Persona/Surface.js'
import * as temporal from '../../../dist/Verification/TemporalSurface.js'

const hash = (value) => `H(${value})`

const root = (session, physicalMessageId, agent) => {
  const identity = persona.resolveParticipantIdentityAtRoot(agent)
  assert.equal(identity.ok, true, identity.ok ? '' : identity.error)

  const created = authority.createAuthorityRoot(
    hash,
    'runtime-session-container',
    session,
    'HumanRoot',
    physicalMessageId,
    {
      kind: 'RootSelection',
      ownerSession: null,
      ownerLogicalRun: null,
      ownerAuthorityRoot: null,
      participantIdentity: {
        selectedAgent: identity.identity.name,
        peerAgent: identity.identity.peer,
        canonicalRole: identity.identity.role,
        selectedTier: identity.identity.initialTier,
        persona: identity.identity.persona,
        personaCatalogVersion: identity.identity.catalogVersion,
        origin: identity.identity.origin,
      },
    },
  )
  assert.equal(created.ok, true, created.ok ? '' : created.error)
  return created.value
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
        InitialTier: `${profile.participantIdentity.selectedTier[0].toUpperCase()}${profile.participantIdentity.selectedTier.slice(1)}`,
        Persona: profile.participantIdentity.persona,
        PersonaCatalogVersion: profile.participantIdentity.personaCatalogVersion,
        Origin: profile.participantIdentity.origin,
      },
    },
  },
})

test('WHAT[SESSION-ONTOLOGY-015] physical SessionId reuse requires durable logical-run closure', () => {
  const session = 'ses-reusable-container'
  const first = root(session, 'msg-container-a', 'fast-manager')
  const second = root(session, 'msg-container-b', 'deep-reviewer')
  const scenario = temporal.sessionReuseIdentityScenario(
    acceptedFact(first),
    acceptedFact(second),
  )

  assert.equal(scenario.preCloseSecond.ok, false)
  assert.match(scenario.preCloseSecond.error.Reason, /active logical run must close before replacement/)
  assert.equal(scenario.afterLife.sessions[session].activeLogicalRun, null)
  assert.equal(scenario.online.sessions[session].activeLogicalRun.session, session)
  assert.equal(scenario.online.sessions[session].activeLogicalRun.logicalRun, second.logicalRun)
  assert.equal(scenario.online.sessions[session].activeLogicalRun.participantIdentity.Role, 'reviewer')
  assert.notEqual(first.logicalRun, second.logicalRun)
  assert.deepEqual(scenario.replayed, scenario.online)
})
