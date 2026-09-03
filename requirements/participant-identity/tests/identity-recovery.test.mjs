import assert from 'node:assert/strict'
import test from 'node:test'

import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as journalCodec from '../../../dist/Persistence/Journal/CodecSurface.js'
import * as factCodec from '../../../dist/Persistence/Journal/FactCodecSurface.js'

const H = (value) => `H(${value})`

const rootSeed = {
  kind: 'RootSelection',
  ownerSession: null,
  ownerLogicalRun: null,
  ownerAuthorityRoot: null,
  participantIdentity: {
    selectedAgent: 'coder',
    peerAgent: 'coder',
    canonicalRole: 'coder',
    selectedTier: 'deep',
    persona: 'Coder',
    personaCatalogVersion: 1,
    origin: 'ResolvedAtRoot',
  },
}

const createRoot = (
  kind = 'HumanRoot',
  seed = rootSeed,
  session = 'ses-recovery',
  physical = 'msg-recovery',
) => {
  const created = authority.createAuthorityRoot(
    H,
    'runtime-identity-recovery',
    session,
    kind,
    physical,
    seed,
  )
  assert.equal(created.ok, true, created.ok ? '' : created.error)
  return created.value
}

const register = (profile) => {
  const projection = authority.registerAuthority(profile, authority.empty)
  assert.equal(projection.activeLogicalRun?.logicalRun, profile.logicalRun)
  return projection
}

const authorityFact = (profile, identitySeed = profile.identitySeed) => ({
  family: 'Prompt',
  case: 'AuthorityRootAccepted',
  payload: {
    SchemaVersion: 2,
    SessionId: profile.session,
    LogicalRunId: profile.logicalRun,
    AuthorityRootUserMessageId: profile.authorityRoot,
    AuthorityKind: profile.authorityKind,
    IdentitySeed: identitySeed,
  },
})

const envelope = (fact) => ({
  runtime: 'runtime-identity-recovery',
  seq: 1,
  observedAt: '2026-08-30T00:00:00Z',
  id: 'identity-recovery-event',
  stream: { kind: 'Session', id: fact.payload.SessionId },
  providerRun: null,
  fact,
})

const journalRoundTripPayload = (fact) => {
  const factDecoded = factCodec.decode(factCodec.encode(fact))
  assert.equal(factDecoded.ok, true, factDecoded.ok ? '' : factDecoded.error)

  const line = journalCodec.serialize(envelope({ ...fact, payload: factDecoded.payload }))
  const decoded = journalCodec.deserialize(line)
  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  return decoded.value.fact.payload
}

const profileFromPayload = (payload) => ({
  session: payload.SessionId,
  logicalRun: payload.LogicalRunId,
  authorityRoot: payload.AuthorityRootUserMessageId,
  authorityKind: payload.AuthorityKind,
  identitySeed: payload.IdentitySeed,
  participantIdentity: payload.IdentitySeed.participantIdentity,
})

const authorityCase = (value) => {
  if (Array.isArray(value)) {
    if (value[0] === 'AuthorityRootAccepted') return value
    for (const item of value) {
      const found = authorityCase(item)
      if (found) return found
    }
  } else if (value && typeof value === 'object') {
    for (const item of Object.values(value)) {
      const found = authorityCase(item)
      if (found) return found
    }
  }
  return null
}

const legacyHumanRootLine = (profile) => {
  const current = JSON.parse(factCodec.encode(authorityFact(profile)))
  const tagged = authorityCase(current)
  assert.notEqual(tagged, null)
  const payload = tagged[1]
  delete payload.SchemaVersion
  delete payload.IdentitySeed
  payload.SelectedAgent = 'coder'
  payload.PeerAgent = 'coder'
  payload.CanonicalRole = 'coder'
  payload.SelectedTier = 'deep'
  return JSON.stringify(current)
}

const inheritedProfile = () => {
  const owner = createRoot()
  const issued = authority.issueInheritedIdentitySeed('reviewer', owner)
  assert.equal(issued.ok, true, issued.ok ? '' : issued.error)
  return createRoot('AgentOwnerRoot', issued.value, 'ses-recovery-child', 'msg-recovery-child')
}

test('WHAT[PID-008] current v2 durable identity recovers exact participant and owner provenance', () => {
  const profile = inheritedProfile()
  const payload = journalRoundTripPayload(authorityFact(profile))
  assert.deepEqual(payload, {
    SchemaVersion: 2,
    SessionId: profile.session,
    LogicalRunId: profile.logicalRun,
    AuthorityRootUserMessageId: profile.authorityRoot,
    AuthorityKind: profile.authorityKind,
    IdentitySeed: profile.identitySeed,
  })

  assert.deepEqual(authority.recoverActiveIdentity(register(profileFromPayload(payload))), {
    ok: true,
    value: {
      participantIdentity: profile.participantIdentity,
      identitySeed: profile.identitySeed,
    },
    error: '',
  })
})

test('WHAT[PID-008] supported legacy HumanRoot deterministically recovers its upgraded identity', () => {
  const profile = createRoot()
  const legacy = legacyHumanRootLine(profile)
  const first = factCodec.decode(legacy)
  const second = factCodec.decode(legacy)
  assert.equal(first.ok, true, first.ok ? '' : first.error)
  assert.equal(second.ok, true, second.ok ? '' : second.error)
  assert.deepEqual(second.payload, first.payload)
  assert.deepEqual(first.payload, {
    SchemaVersion: 2,
    SessionId: profile.session,
    LogicalRunId: profile.logicalRun,
    AuthorityRootUserMessageId: profile.authorityRoot,
    AuthorityKind: profile.authorityKind,
    IdentitySeed: rootSeed,
  })

  assert.deepEqual(authority.recoverActiveIdentity(register(profileFromPayload(first.payload))), {
    ok: true,
    value: {
      participantIdentity: rootSeed.participantIdentity,
      identitySeed: rootSeed,
    },
    error: '',
  })
})

test('WHAT[PID-008] missing active authority rejects even when LastAuthorityProfile is present', () => {
  const projection = register(createRoot())
  const historicalOnly = { ...projection, activeLogicalRun: null }

  assert.deepEqual(authority.recoverActiveIdentity(historicalOnly), {
    ok: false,
    value: null,
    error: 'MissingActiveAuthority',
  })
})

test('WHAT[PID-008] rejects corrupt identity provenance', () => {
  const rootProjection = register(createRoot())
  const inheritedProjection = register(inheritedProfile())
  const corruptions = [
    [rootProjection, (active) => { active.identitySeed.participantIdentity.persona = 'Engineer' }],
    [rootProjection, (active) => { active.identitySeed.participantIdentity.personaCatalogVersion = 99 }],
    [inheritedProjection, (active) => { active.identitySeed.participantIdentity.origin = 'ResolvedAtRoot' }],
    [inheritedProjection, (active) => { active.identitySeed.ownerSession = '' }],
    [inheritedProjection, (active) => { active.identitySeed.ownerLogicalRun = '' }],
    [inheritedProjection, (active) => { active.identitySeed.ownerAuthorityRoot = '' }],
  ]

  for (const [projection, corrupt] of corruptions) {
    const candidate = structuredClone(projection)
    corrupt(candidate.activeLogicalRun)
    const recovered = authority.recoverActiveIdentity(candidate)
    assert.equal(recovered.ok, false)
    assert.equal(recovered.value, null)
    assert.notEqual(recovered.error, '')
  }
})

test('WHAT[PID-008] closed exact run cannot be recovered as current', () => {
  const profile = createRoot()
  const closed = authority.closeAuthority(
    profile.logicalRun,
    profile.authorityRoot,
    register(profile),
  )
  assert.equal(closed.ok, true, closed.ok ? '' : closed.error)
  assert.notEqual(closed.value.lastAuthorityProfile, null)
  assert.equal(closed.value.activeLogicalRun, null)

  assert.deepEqual(authority.recoverActiveIdentity(closed.value), {
    ok: false,
    value: null,
    error: 'MissingActiveAuthority',
  })
})
