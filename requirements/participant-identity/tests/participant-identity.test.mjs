// ParticipantIdentity algebra proof through its JS-native production boundary.

import assert from 'node:assert/strict'
import test from 'node:test'
import { assertJsData } from '../../verification-system/tests/support/js-contract.mjs'

const identity = await import('../../../dist/Participant/Persona/Surface.js')

const EXPECTED = {
  'fast-orchestrator': ['orchestrator', 'fast', 'deep-orchestrator', 'Integrator'],
  'deep-orchestrator': ['orchestrator', 'deep', 'fast-orchestrator', 'Director'],
  'fast-manager': ['manager', 'fast', 'deep-manager', 'Coordinator'],
  'deep-manager': ['manager', 'deep', 'fast-manager', 'Lead'],
  'fast-coder': ['coder', 'fast', 'deep-coder', 'Coder'],
  'deep-coder': ['coder', 'deep', 'fast-coder', 'Engineer'],
  'fast-inspector': ['inspector', 'fast', 'deep-inspector', 'Scout'],
  'deep-inspector': ['inspector', 'deep', 'fast-inspector', 'Investigator'],
  'fast-devops': ['devops', 'fast', 'deep-devops', 'Technician'],
  'deep-devops': ['devops', 'deep', 'fast-devops', 'Operator'],
  'fast-browser': ['browser', 'fast', 'deep-browser', 'Navigator'],
  'deep-browser': ['browser', 'deep', 'fast-browser', 'Researcher'],
  'fast-inquiry': ['inquiry', 'fast', 'deep-inquiry', 'Analyst'],
  'deep-inquiry': ['inquiry', 'deep', 'fast-inquiry', 'Inquirer'],
  'fast-reviewer': ['reviewer', 'fast', 'deep-reviewer', 'Examiner'],
  'deep-reviewer': ['reviewer', 'deep', 'fast-reviewer', 'Auditor'],
  'fast-blogger': ['blogger', 'fast', 'deep-blogger', 'Scribe'],
  'deep-blogger': ['blogger', 'deep', 'fast-blogger', 'Chronicler'],
  'fast-distiller': ['distiller', 'fast', 'deep-distiller', 'Condenser'],
  'deep-distiller': ['distiller', 'deep', 'fast-distiller', 'Distiller'],
  'fast-bookkeeper': ['bookkeeper', 'fast', 'deep-bookkeeper', 'Clerk'],
  'deep-bookkeeper': ['bookkeeper', 'deep', 'fast-bookkeeper', 'Curator'],
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
  for (const name of ['reviewer-fast', 'fast_reviewer']) {
    assertError(identity.resolveParticipantIdentityAtRoot(name), 'LegacyParticipantName')
  }
  for (const name of ['', '   ', null]) {
    assertError(identity.resolveParticipantIdentityAtRoot(name), 'BlankParticipantName')
  }
  for (const name of ['fast-', 'manager-fast-extra']) {
    assertError(identity.resolveParticipantIdentityAtRoot(name), 'MalformedParticipantName')
  }
  assertError(identity.resolveParticipantIdentityAtRoot('fast-unknown'), 'UnknownParticipantName')
})

test('WHAT[PID-003] rejects blank Persona and unsupported catalog version', () => {
  assertError(rehydrate({ ...expectedView('fast-coder'), persona: '  ' }), 'BlankPersona')
  assertError(
    rehydrate({ ...expectedView('fast-coder'), catalogVersion: 2 }),
    'UnsupportedPersonaCatalogVersion',
  )
})

test('WHAT[PID-001] rejects independently supplied role, tier, peer, persona, and origin', () => {
  const canonical = expectedView('fast-coder')
  const mismatches = [
    [{ ...canonical, role: 'reviewer' }, 'RoleMismatch'],
    [{ ...canonical, initialTier: 'deep' }, 'TierMismatch'],
    [{ ...canonical, peer: 'deep-reviewer' }, 'PeerMismatch'],
    [{ ...canonical, persona: 'Engineer' }, 'PersonaMismatch'],
    [{ ...canonical }, 'OriginMismatch'],
  ]

  for (const [input, error] of mismatches) {
    assertError(rehydrate(input, error === 'OriginMismatch' ? 'fast-coder' : ''), error)
  }
})

test('WHAT[PID-010] inherited identity requires the exact current owner Persona and version', () => {
  const inherited = identity.inheritParticipantIdentityFromOwner('fast-coder', 'deep-manager')
  assertJsData(inherited, 'inherited identity')
  assert.equal(inherited.ok, true)
  assert.equal(inherited.error, null)
  assert.deepEqual(inherited.identity, {
    ...expectedView('fast-coder', 'InheritedFromOwner'),
    persona: 'Lead',
  })

  const restored = rehydrate(inherited.identity, 'deep-manager')
  assertJsData(restored, 'rehydrated inherited identity')
  assert.equal(restored.ok, true)
  assert.deepEqual(restored.identity, inherited.identity)

  assertError(rehydrate(inherited.identity), 'OwnerRequired')
  assertError(
    rehydrate({ ...inherited.identity, persona: 'Coordinator' }, 'deep-manager'),
    'OwnerPersonaMismatch',
  )
  assertError(
    rehydrate({ ...inherited.identity, catalogVersion: 2 }, 'deep-manager'),
    'UnsupportedPersonaCatalogVersion',
  )
  assertError(rehydrate(inherited.identity, 'fast-manager'), 'OwnerPersonaMismatch')
})
