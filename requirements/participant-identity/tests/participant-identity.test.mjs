// ParticipantIdentity algebra proof through its JS-native production boundary.
// Current assertions use only canonical participant+Role/Persona/provenance
// (name, role, persona, catalogVersion, origin). The production boundary keeps
// positional compat slots for the retired initialTier/peer inputs; this file
// passes explicit legacy values in those slots once to prove they are ignored
// and never affect the resolved identity.

import assert from 'node:assert/strict'
import test from 'node:test'
import { assertJsData } from '../../verification-system/tests/support/js-contract.mjs'

const identity = await import('../../../dist/Participant/Persona/Surface.js')

const EXPECTED = {
  orchestrator: { role: 'orchestrator', persona: 'Director' },
  manager: { role: 'manager', persona: 'Lead' },
  coder: { role: 'coder', persona: 'Coder' },
  inspector: { role: 'inspector', persona: 'Investigator' },
  devops: { role: 'devops', persona: 'Operator' },
  browser: { role: 'browser', persona: 'Researcher' },
  inquiry: { role: 'inquiry', persona: 'Analyst' },
  blogger: { role: 'blogger', persona: 'Chronicler' },
  distiller: { role: 'distiller', persona: 'Distiller' },
  bookkeeper: { role: 'bookkeeper', persona: 'Curator' },
  predictor: { role: 'inspector', persona: 'Investigator' },
}

const expectedView = (name, origin = 'ResolvedAtRoot') => ({
  name,
  role: EXPECTED[name].role,
  persona: EXPECTED[name].persona,
  catalogVersion: 1,
  origin,
})

const assertCanonicalIdentity = (actual, expected, label) => {
  assert.equal(actual.name, expected.name, `${label} participant`)
  assert.equal(actual.role, expected.role, `${label} role`)
  assert.equal(actual.persona, expected.persona, `${label} persona`)
  assert.equal(actual.catalogVersion, expected.catalogVersion, `${label} version`)
  assert.equal(actual.origin, expected.origin, `${label} origin`)
}

const rehydrate = (view, ownerName = '') =>
  identity.rehydrateParticipantIdentity(
    ownerName,
    view.name,
    view.role,
    'deep',
    'retired-peer-slot',
    view.persona,
    view.catalogVersion,
    view.origin,
  )

const assertError = (result, error) => {
  assertJsData(result, error)
  assert.equal(result.ok, false)
  assert.equal(result.identity, null)
  assert.equal(result.error, error)
}

test('WHAT[PID-001] resolves every canonical participant identity and persona', () => {
  assert.deepEqual(new Set(identity.requiredNames), new Set(Object.keys(EXPECTED)))

  for (const name of identity.requiredNames) {
    const result = identity.resolveParticipantIdentityAtRoot(name)
    assertJsData(result, name)
    assert.equal(result.ok, true, name)
    assert.equal(result.error, null, name)
    assertCanonicalIdentity(result.identity, expectedView(name), name)

    const restored = rehydrate(result.identity)
    assertJsData(restored, `${name} rehydration`)
    assert.equal(restored.ok, true, name)
    assertCanonicalIdentity(restored.identity, expectedView(name), `${name} rehydrated`)
  }
})

test('WHAT[PID-001] rejects legacy, malformed, blank, and unknown participant names', () => {
  for (const name of identity.legacyNames) {
    assertError(identity.resolveParticipantIdentityAtRoot(name), 'LegacyParticipantName')
  }
  for (const name of ['fast_reviewer']) {
    assertError(identity.resolveParticipantIdentityAtRoot(name), 'LegacyParticipantName')
  }
  for (const name of ['', '   ', null]) {
    assertError(identity.resolveParticipantIdentityAtRoot(name), 'BlankParticipantName')
  }
  for (const name of ['reviewer-fast', 'fast-', 'manager-fast-extra', 'coder-deep']) {
    assertError(identity.resolveParticipantIdentityAtRoot(name), 'MalformedParticipantName')
  }
  assertError(identity.resolveParticipantIdentityAtRoot('unknown'), 'UnknownParticipantName')
})

test('WHAT[PID-003] rejects blank Persona and unsupported catalog version', () => {
  assertError(rehydrate({ ...expectedView('coder'), persona: '  ' }), 'BlankPersona')
  assertError(
    rehydrate({ ...expectedView('coder'), catalogVersion: 2 }),
    'UnsupportedPersonaCatalogVersion',
  )
})

test('WHAT[PID-001] rejects independently supplied role, persona, and origin', () => {
  const canonical = expectedView('coder')
  const mismatches = [
    [{ ...canonical, role: 'devops' }, 'RoleMismatch'],
    [{ ...canonical, persona: 'Lead' }, 'PersonaMismatch'],
    [{ ...canonical }, 'OriginMismatch'],
  ]

  for (const [input, error] of mismatches) {
    assertError(rehydrate(input, error === 'OriginMismatch' ? 'coder' : ''), error)
  }
})

test('WHAT[PID-002] the retired peer slot is ignored and never affects identity', () => {
  const canonical = expectedView('coder')
  const withLegacyPeer = identity.rehydrateParticipantIdentity(
    '',
    canonical.name,
    canonical.role,
    'fast',
    'some-other-peer',
    canonical.persona,
    canonical.catalogVersion,
    canonical.origin,
  )
  assert.equal(withLegacyPeer.ok, true)
  assertCanonicalIdentity(withLegacyPeer.identity, canonical, 'peer-slot-ignored')
})

test('WHAT[PID-008] inherited identity requires the exact current owner Persona and version', () => {
  const inherited = identity.inheritParticipantIdentityFromOwner('coder', 'manager')
  assertJsData(inherited, 'inherited identity')
  assert.equal(inherited.ok, true)
  assert.equal(inherited.error, null)
  assertCanonicalIdentity(
    inherited.identity,
    { ...expectedView('coder', 'InheritedFromOwner'), persona: 'Lead' },
    'inherited',
  )

  const restored = rehydrate(inherited.identity, 'manager')
  assertJsData(restored, 'rehydrated inherited identity')
  assert.equal(restored.ok, true)
  assertCanonicalIdentity(restored.identity, { ...expectedView('coder', 'InheritedFromOwner'), persona: 'Lead' }, 'rehydrated inherited')

  assertError(rehydrate(inherited.identity), 'OwnerRequired')
  assertError(
    rehydrate({ ...inherited.identity, persona: 'Director' }, 'manager'),
    'OwnerPersonaMismatch',
  )
  assertError(
    rehydrate({ ...inherited.identity, catalogVersion: 2 }, 'manager'),
    'UnsupportedPersonaCatalogVersion',
  )
  assertError(rehydrate(inherited.identity, 'orchestrator'), 'OwnerPersonaMismatch')
})
