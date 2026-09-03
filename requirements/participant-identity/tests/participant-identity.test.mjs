// ParticipantIdentity algebra proof through its JS-native production boundary.

import assert from 'node:assert/strict'
import test from 'node:test'
import { assertJsData } from '../../verification-system/tests/support/js-contract.mjs'

const identity = await import('../../../dist/Participant/Persona/Surface.js')

const EXPECTED = {
  orchestrator: ['orchestrator', 'deep', 'orchestrator', 'Director'],
  manager: ['manager', 'deep', 'manager', 'Lead'],
  coder: ['coder', 'deep', 'coder', 'Coder'],
  inspector: ['inspector', 'deep', 'inspector', 'Investigator'],
  devops: ['devops', 'deep', 'devops', 'Operator'],
  browser: ['browser', 'deep', 'browser', 'Researcher'],
  inquiry: ['inquiry', 'deep', 'inquiry', 'Analyst'],
  reviewer: ['reviewer', 'deep', 'reviewer', 'Auditor'],
  blogger: ['blogger', 'deep', 'blogger', 'Chronicler'],
  distiller: ['distiller', 'deep', 'distiller', 'Distiller'],
  bookkeeper: ['bookkeeper', 'deep', 'bookkeeper', 'Curator'],
  predictor: ['inspector', 'deep', 'predictor', 'Investigator'],
}

const expectedView = (name, origin = 'ResolvedAtRoot') => {
  const [role, initialTier, peer, persona] = EXPECTED[name]
  return { name, role, initialTier, peer, persona, catalogVersion: 1, origin }
}

const rehydrate = (view, ownerName = '') => identity.rehydrateParticipantIdentity(
  ownerName,
  view.name,
  view.role,
  view.initialTier,
  view.peer,
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
    assert.deepEqual(result.identity, expectedView(name), name)

    const restored = rehydrate(result.identity)
    assertJsData(restored, `${name} rehydration`)
    assert.equal(restored.ok, true, name)
    assert.deepEqual(restored.identity, result.identity, name)
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
    [{ ...canonical, role: 'reviewer' }, 'RoleMismatch'],
    [{ ...canonical, persona: 'Lead' }, 'PersonaMismatch'],
    [{ ...canonical }, 'OriginMismatch'],
  ]

  for (const [input, error] of mismatches) {
    assertError(rehydrate(input, error === 'OriginMismatch' ? 'coder' : ''), error)
  }
})

test('WHAT[PID-008] inherited identity requires the exact current owner Persona and version', () => {
  const inherited = identity.inheritParticipantIdentityFromOwner('coder', 'manager')
  assertJsData(inherited, 'inherited identity')
  assert.equal(inherited.ok, true)
  assert.equal(inherited.error, null)
  assert.deepEqual(inherited.identity, {
    ...expectedView('coder', 'InheritedFromOwner'),
    persona: 'Lead',
  })

  const restored = rehydrate(inherited.identity, 'manager')
  assertJsData(restored, 'rehydrated inherited identity')
  assert.equal(restored.ok, true)
  assert.deepEqual(restored.identity, inherited.identity)

  assertError(rehydrate(inherited.identity), 'OwnerRequired')
  assertError(
    rehydrate({ ...inherited.identity, persona: 'Director' }, 'manager'),
    'OwnerPersonaMismatch',
  )
  assertError(
    rehydrate({ ...inherited.identity, catalogVersion: 2 }, 'manager'),
    'UnsupportedPersonaCatalogVersion',
  )
  assertError(rehydrate(inherited.identity, 'reviewer'), 'OwnerPersonaMismatch')
})
