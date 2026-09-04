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
      selectedAgent: resolved.identity.name,
      peerAgent: resolved.identity.peer,
      canonicalRole: resolved.identity.role,
      selectedTier: resolved.identity.initialTier.toLowerCase(),
      persona: resolved.identity.persona,
      personaCatalogVersion: resolved.identity.catalogVersion,
      origin: resolved.identity.origin,
    },
  }
}

const rootProfile = (
  session = 'ses_owner',
  physical = 'msg_owner_root',
  agent = 'manager',
) => {
  const result = authority.createAuthorityRoot(
    H,
    'runtime-identity-lineage',
    session,
    'HumanRoot',
    physical,
    rootSelection(agent),
  )
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.value
}

const inheritedSeed = (child, owner) => {
  const result = authority.issueInheritedIdentitySeed(child, owner)
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.value
}

test('WHAT[PID-008] inherited identity records the exact durable owner witness', () => {
  const owner = rootProfile()
  const seed = inheritedSeed('coder', owner)

  assert.deepEqual(
    {
      kind: seed.kind,
      ownerSession: seed.ownerSession,
      ownerLogicalRun: seed.ownerLogicalRun,
      ownerAuthorityRoot: seed.ownerAuthorityRoot,
    },
    {
      kind: 'InheritedFromOwner',
      ownerSession: owner.session,
      ownerLogicalRun: owner.logicalRun,
      ownerAuthorityRoot: owner.authorityRoot,
    },
  )
  assert.deepEqual(
    {
      selectedAgent: seed.participantIdentity.selectedAgent,
      canonicalRole: seed.participantIdentity.canonicalRole,
      selectedTier: seed.participantIdentity.selectedTier,
      persona: seed.participantIdentity.persona,
      personaCatalogVersion: seed.participantIdentity.personaCatalogVersion,
      origin: seed.participantIdentity.origin,
    },
    {
      selectedAgent: 'coder',
      canonicalRole: 'coder',
      selectedTier: 'deep',
      persona: owner.participantIdentity.persona,
      personaCatalogVersion: owner.participantIdentity.personaCatalogVersion,
      origin: 'InheritedFromOwner',
    },
  )
})

test('WHAT[PID-008] rejects stale owner identity evidence', () => {
  const seed = inheritedSeed('inspector', rootProfile())
  const currentOwnerRun = rootProfile('ses_owner', 'msg_fresh_owner_root')

  const validation = authority.validateInheritedIdentitySeed(currentOwnerRun, seed)

  assert.equal(validation.ok, false)
  assert.deepEqual(validation.error, {
    kind: 'OwnerLogicalRunIdMismatch',
    expected: currentOwnerRun.logicalRun,
    actual: seed.ownerLogicalRun,
  })
})

test('WHAT[PID-008] closed owner run rejects its inherited identity evidence', () => {
  const owner = rootProfile()
  const seed = inheritedSeed('inspector', owner)

  const validation = authority.validateInheritedIdentitySeedAgainstActiveOwner(null, seed)

  assert.equal(validation.ok, false)
  assert.deepEqual(validation.error, {
    kind: 'OwnerAuthorityNotActive',
    expected: owner.session,
    actual: '',
  })
})

test('WHAT[PID-008] derived identity rejects root-selection evidence', () => {
  const owner = rootProfile()

  const validation = authority.validateInheritedIdentitySeed(owner, owner.identitySeed)

  assert.equal(validation.ok, false)
  assert.deepEqual(validation.error, {
    kind: 'ExpectedInheritedFromOwner',
    expected: 'InheritedFromOwner',
    actual: 'RootSelection',
  })
})

test('WHAT[PID-008] inherited identity rejects a different owner session', () => {
  const owner = rootProfile()
  const seed = inheritedSeed('inspector', owner)
  const wrongOwner = { ...owner, session: 'ses_different_owner' }

  const validation = authority.validateInheritedIdentitySeed(wrongOwner, seed)

  assert.equal(validation.ok, false)
  assert.deepEqual(validation.error, {
    kind: 'OwnerSessionIdMismatch',
    expected: wrongOwner.session,
    actual: owner.session,
  })
})

test('WHAT[PID-008] inherited identity rejects a different authority root', () => {
  const owner = rootProfile()
  const seed = inheritedSeed('inspector', owner)
  const wrongRoot = { ...owner, authorityRoot: 'msg_different_root' }

  const validation = authority.validateInheritedIdentitySeed(wrongRoot, seed)

  assert.equal(validation.ok, false)
  assert.deepEqual(validation.error, {
    kind: 'OwnerAuthorityRootUserMessageIdMismatch',
    expected: wrongRoot.authorityRoot,
    actual: owner.authorityRoot,
  })
})

test('WHAT[PID-008] durable inherited seed round-trips without re-resolution', () => {
  const owner = rootProfile()
  const seed = inheritedSeed('inspector', owner)
  const claimed = authority.claimAgentOwnerRoot('pk_child', 'ses_child', 'digest-child', seed)
  assert.equal(claimed.ok, true, claimed.ok ? '' : claimed.error)

  const replayedClaim = JSON.parse(JSON.stringify(claimed.value))
  assert.deepEqual(authority.projectClaimIdentitySeed(replayedClaim), seed)

  const serialized = authority.serializeIdentitySeed(
    authority.projectClaimIdentitySeed(replayedClaim),
  )
  assert.equal(serialized.ok, true, serialized.ok ? '' : serialized.error)
  const replayed = authority.rehydrateIdentitySeed(serialized.value)

  assert.equal(replayed.ok, true, replayed.ok ? '' : replayed.error)
  assert.deepEqual(replayed.value, seed)
  assert.deepEqual(authority.validateInheritedIdentitySeed(owner, replayed.value), {
    ok: true,
    value: seed.participantIdentity,
    error: null,
  })
})
