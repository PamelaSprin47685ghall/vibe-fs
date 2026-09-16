import assert from 'node:assert/strict'
import test from 'node:test'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as persona from '../../../dist/Participant/Persona/Surface.js'

const H = (value) => `H(${value})`
const rootSelection = (agent) => {
  const resolved = persona.resolveParticipantIdentityAtRoot(agent)
  assert.equal(resolved.ok, true, resolved.ok ? '' : resolved.error)
  return {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: {
      participant: resolved.identity.name,
      role: resolved.identity.role,
      persona: resolved.identity.persona,
      personaCatalogVersion: resolved.identity.catalogVersion,
      origin: resolved.identity.origin,
    },
  }
}

test('WHAT[PID-009] production plugin replaces identity only after exact durable Manager closure', () => {
  const created = authority.createAuthorityRoot(
    H,
    'runtime-reuse-closure',
    'ses_reuse_owner',
    'HumanRoot',
    'msg_reuse_owner',
    rootSelection('manager'),
  )
  assert.equal(created.ok, true)
})

test('WHAT[PID-009] reuses SessionId with a fresh closed-run identity', () => {
  const created = authority.createAuthorityRoot(
    H,
    'runtime-reuse-identity',
    'ses_reuse_identity',
    'HumanRoot',
    'msg_reuse_identity',
    rootSelection('manager'),
  )
  assert.equal(created.ok, true)
})
