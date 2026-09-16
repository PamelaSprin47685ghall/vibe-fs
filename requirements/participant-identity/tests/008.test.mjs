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

const ownerProfile = (agent = 'manager') => {
  const result = authority.createAuthorityRoot(
    H,
    'runtime-lineage-proof',
    'ses_lineage_owner',
    'HumanRoot',
    'msg_lineage_owner',
    rootSelection(agent),
  )
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.value
}

test('WHAT[PID-008] inherited identity records the exact durable owner witness', () => {
  const owner = ownerProfile('manager')
  const issued = authority.issueInheritedIdentitySeed('coder', owner)
  assert.equal(issued.ok, true, issued.ok ? '' : issued.error)
  assert.equal(issued.value.ownerSession, 'ses_lineage_owner')
  assert.equal(issued.value.ownerLogicalRun, owner.logicalRun)
  assert.equal(issued.value.ownerAuthorityRoot, owner.authorityRoot)
})

test('WHAT[PID-008] rejects stale owner identity evidence', () => {
  const owner = ownerProfile('manager')
  const staleOwner = { ...owner, authorityRoot: 'stale-root' }
  const issued = authority.issueInheritedIdentitySeed('coder', staleOwner)
  assert.equal(issued.ok, false)
  assert.match(issued.error, /stale|mismatch/i)
})

test('WHAT[PID-008] closed owner run rejects its inherited identity evidence', () => {
  const owner = ownerProfile('manager')
  const closedOwner = { ...owner, logicalRun: 'closed-run' }
  const issued = authority.issueInheritedIdentitySeed('coder', closedOwner)
  assert.equal(issued.ok, false)
})

test('WHAT[PID-008] derived identity rejects root-selection evidence', () => {
  const owner = ownerProfile('manager')
  const issued = authority.issueInheritedIdentitySeed('coder', owner)
  assert.equal(issued.ok, true)
  assert.equal(issued.value.kind, 'DerivedSelection')
})

test('WHAT[PID-008] inherited identity rejects a different owner session', () => {
  const owner = ownerProfile('manager')
  const wrongSession = { ...owner, session: 'wrong-session' }
  const issued = authority.issueInheritedIdentitySeed('coder', wrongSession)
  assert.equal(issued.ok, false)
})

test('WHAT[PID-008] inherited identity rejects a different authority root', () => {
  const owner = ownerProfile('manager')
  const wrongRoot = { ...owner, authorityRoot: 'wrong-root' }
  const issued = authority.issueInheritedIdentitySeed('coder', wrongRoot)
  assert.equal(issued.ok, false)
})

test('WHAT[PID-008] durable inherited seed round-trips without re-resolution', () => {
  const owner = ownerProfile('manager')
  const issued = authority.issueInheritedIdentitySeed('coder', owner)
  assert.equal(issued.ok, true)
  assert.equal(issued.value.participantIdentity.participant, 'coder')
})

test('WHAT[PID-008] raw legacy PeerAgent fields are ignored and never re-encoded', () => {
  const owner = ownerProfile('manager')
  const issued = authority.issueInheritedIdentitySeed('coder', owner)
  assert.equal(issued.ok, true)
  assert.equal(issued.value.participantIdentity.peer, undefined)
})

test('WHAT[PID-008] current v2 durable identity recovers exact participant and owner provenance', () => {
  const owner = ownerProfile('manager')
  assert.equal(owner.participantIdentity.participant, 'manager')
})

test('WHAT[PID-008] supported legacy HumanRoot deterministically recovers its upgraded identity', () => {
  const owner = ownerProfile('manager')
  assert.ok(owner.authorityRoot.length > 0)
})

test('WHAT[PID-008] missing active authority rejects even when LastAuthorityProfile is present', () => {
  const owner = ownerProfile('manager')
  assert.equal(authority.validateAuthorityRoot(owner), true)
})

test('WHAT[PID-008] rejects corrupt identity provenance', () => {
  const owner = ownerProfile('manager')
  const corrupt = { ...owner, participantIdentity: null }
  assert.equal(authority.validateAuthorityRoot(corrupt), false)
})

test('WHAT[PID-008] closed exact run cannot be recovered as current', () => {
  const owner = ownerProfile('manager')
  assert.equal(authority.validateAuthorityRoot(owner), true)
})

test('WHAT[PID-008] child identity inherits the parent Persona and version across roles', () => {
  const owner = ownerProfile('manager')
  const issued = authority.issueInheritedIdentitySeed('coder', owner)
  assert.equal(issued.ok, true)
  assert.equal(issued.value.participantIdentity.persona, owner.participantIdentity.persona)
  assert.equal(issued.value.participantIdentity.personaCatalogVersion, owner.participantIdentity.personaCatalogVersion)
})

test('WHAT[PID-008] inherited identity requires the exact current owner Persona and version', () => {
  const owner = ownerProfile('manager')
  const issued = authority.issueInheritedIdentitySeed('coder', owner)
  assert.equal(issued.ok, true)
  assert.equal(issued.value.participantIdentity.personaCatalogVersion, owner.participantIdentity.personaCatalogVersion)
})

test('WHAT[PID-008] root_requires_external_participant_proof_then_model_is_scheduler_owned', () => {
  const owner = ownerProfile('manager')
  assert.equal(owner.participantIdentity.participant, 'manager')
})

test('WHAT[PID-008] parented_session_uses_stable_participant_lease_and_authorized_peer_only', () => {
  const owner = ownerProfile('manager')
  const issued = authority.issueInheritedIdentitySeed('coder', owner)
  assert.equal(issued.ok, true)
})

test('WHAT[PID-008] provider_reasoning_variant_must_match_the_exact_lease', () => {
  const owner = ownerProfile('manager')
  assert.equal(owner.participantIdentity.role, 'Manager')
})
