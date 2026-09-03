import assert from 'node:assert/strict'
import test from 'node:test'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'

const hash = (value) => `H(${value})`

const rootSelection = (participantIdentity) => ({
  kind: 'RootSelection',
  ownerSession: null,
  ownerLogicalRun: null,
  ownerAuthorityRoot: null,
  participantIdentity,
})

const coderIdentity = {
  selectedAgent: 'coder',
  peerAgent: 'coder',
  canonicalRole: 'coder',
  selectedTier: 'deep',
  persona: 'Coder',
  personaCatalogVersion: 1,
  origin: 'ResolvedAtRoot',
}

const createRoot = () => {
  const result = authority.createAuthorityRoot(
    hash,
    'rt-profile',
    'ses-profile',
    'HumanRoot',
    'msg-profile',
    rootSelection(coderIdentity),
  )
  assert.equal(result.ok, true, result.error)
  return result.value
}

const identityOf = (profile) => profile.identitySeed.participantIdentity

test('WHAT[INTERACTION-AUTHORITY-003] valid authority profiles carry one atomic participant identity', () => {
  const profile = createRoot()

  assert.deepEqual(
    {
      session: profile.session,
      logicalRun: profile.logicalRun,
      authorityRoot: profile.authorityRoot,
      authorityKind: profile.authorityKind,
    },
    {
      session: 'ses-profile',
      logicalRun: 'H(rt-profile\nses-profile\nmsg-profile)',
      authorityRoot: 'msg-profile',
      authorityKind: 'HumanRoot',
    },
  )
  assert.deepEqual(profile.identitySeed, rootSelection(coderIdentity))
  assert.deepEqual(profile.participantIdentity, identityOf(profile))
  for (const field of Object.keys(profile.participantIdentity)) {
    assert.equal(Object.hasOwn(profile, field), false, `${field} was duplicated outside participantIdentity`)
  }
})

test('WHAT[INTERACTION-AUTHORITY-003] rejects hand-built mismatched profile', () => {
  const valid = createRoot()
  const mismatches = {
    selectedAgent: 'reviewer',
    canonicalRole: 'reviewer',
    persona: 'Auditor',
  }

  for (const [field, value] of Object.entries(mismatches)) {
    const result = authority.registerAuthority(
      {
        ...valid,
        identitySeed: {
          ...valid.identitySeed,
          participantIdentity: { ...identityOf(valid), [field]: value },
        },
      },
      authority.empty,
    )
    assert.equal(result.ok, false, `${field} mismatch was accepted`)
  }
})

test('WHAT[INTERACTION-AUTHORITY-003] Bookkeeper cannot enter a public authority profile', () => {
  const result = authority.createAuthorityRoot(
    hash,
    'rt-profile',
    'ses-profile',
    'HumanRoot',
    'msg-profile',
    rootSelection({
      selectedAgent: 'bookkeeper',
      peerAgent: 'bookkeeper',
      canonicalRole: 'bookkeeper',
      selectedTier: 'deep',
      persona: 'Curator',
      personaCatalogVersion: 1,
      origin: 'ResolvedAtRoot',
    }),
  )

  assert.equal(result.ok, false)
})
