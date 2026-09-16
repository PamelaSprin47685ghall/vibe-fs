// requirements/participant-identity/tests/catalog.test.mjs
//
// AGENT-001/002/003/004: managed identity is exercised through the
// Participant/Persona JS-native surface. Role and legacy rejection are
// semantic vocabulary; their F# DU representation is not a test contract.

import assert from 'node:assert/strict'
import test from 'node:test'
import { assertJsData } from '../../verification-system/tests/support/js-contract.mjs'

const identity = await import('../../../dist/Participant/Persona/Surface.js')

const EXPECTED_ROLES = [
  'manager',
  'orchestrator',
  'coder',
  'inspector',
  'browser',
  'inquiry',
  'devops',
  'distiller',
  'blogger',
]

const EXPECTED_LEGACY = [
  'build',
  'plan',
  'student',
  'teacher',
  'meditator',
  'executor',
]

const EXPECTED_PERSONAS = {
  orchestrator: 'Director',
  manager: 'Lead',
  coder: 'Coder',
  inspector: 'Investigator',
  devops: 'Operator',
  browser: 'Researcher',
  inquiry: 'Analyst',
  blogger: 'Chronicler',
  distiller: 'Distiller',
}

test('WHAT[PID-001] catalog_has_exactly_ten_canonical_roles', () => {
  assertJsData(identity.allRoleLabels, 'allRoleLabels')
  assert.deepEqual([...identity.allRoleLabels].sort(), [...EXPECTED_ROLES].sort())
  assert.equal(identity.allRoleLabels.length, 9)
  assert.equal(identity.allPublicRoleLabels.length + identity.allInternalRoleLabels.length, 9)
  assert.deepEqual(
    [...identity.allPublicRoleLabels, ...identity.allInternalRoleLabels].sort(),
    [...EXPECTED_ROLES].sort(),
  )
})

test('WHAT[PID-001] required_names_are_canonical_and_include_twelve_agents', () => {
  assertJsData(identity.requiredNames, 'requiredNames')
  assert.equal(identity.requiredNames.length, 11)
  for (const role of EXPECTED_ROLES) {
    assert.equal(identity.isManagedName(role), true)
  }
  assert.equal(identity.isManagedName('bookkeeper'), true)
  assert.equal(identity.isManagedName('predictor'), true)
})

test('WHAT[PID-002] persona_catalog_maps_roles_to_single_persona', () => {
  for (const [role, expected] of Object.entries(EXPECTED_PERSONAS)) {
    assert.equal(personaLabel(role), expected)
  }
  assert.equal(identity.bookkeeperPersona(''), 'Curator')
  assert.equal(identity.resolveParticipantIdentityAtRoot('bookkeeper').identity.persona, 'Curator')
  assert.equal(identity.resolveParticipantIdentityAtRoot('predictor').identity.persona, 'Investigator')
  assert.equal(
    identity.resolveParticipantIdentityAtRoot('predictor').identity.persona,
    personaLabel('inspector'),
  )
})

test('WHAT[PID-002] all_legacy_bare_names_are_rejected', () => {
  assert.deepEqual(new Set(identity.legacyNames), new Set(EXPECTED_LEGACY))
  for (const bare of EXPECTED_LEGACY) {
    assert.equal(identity.isLegacyName(bare), true, `'${bare}' must be legacy`)
    assert.equal(identity.isManagedName(bare), false, `'${bare}' must not parse as managed`)
  }
  assert.equal(identity.isLegacyName('fast_reviewer'), true)
  assert.equal(identity.isManagedName('fast_reviewer'), false)
  assert.equal(identity.isLegacyName('reviewer-fast'), false)
  assert.equal(identity.isManagedName('reviewer-fast'), false)
  for (const name of identity.requiredNames) assert.equal(identity.isLegacyName(name), false)
})

test('WHAT[PID-002] rejection_prose_is_version_agnostic', () => {
  const supported = identity.formatLegacyNameNotSupported('student')
  const inConfig = identity.formatLegacyNameInConfig('student')
  for (const text of [supported, inConfig]) {
    assert.doesNotMatch(text, /0\.5\.\d/)
    assert.doesNotMatch(text, /Wanxiangshu\s+0\.5\.0/)
  }
  assert.equal(
    supported,
    "Legacy agent name 'student' is not supported.",
  )
  assert.equal(
    inConfig,
    "Legacy agent name 'student' is present in opencode.json.",
  )
})

const personaLabel = (role) => identity.persona(role, '')
