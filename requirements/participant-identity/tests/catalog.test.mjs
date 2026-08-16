// requirements/participant-identity/tests/catalog.test.mjs
//
// AGENT-001/002/003/004: managed identity is exercised through the
// Participant/Persona JS-native surface. Role, tier, and legacy rejection are
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
  'reviewer',
  'devops',
  'distiller',
  'blogger',
]

const EXPECTED_LEGACY = [
  'orchestrator',
  'manager',
  'build',
  'plan',
  'coder',
  'inspector',
  'devops',
  'browser',
  'meditator',
  'inquiry',
  'reviewer',
  'student',
  'teacher',
  'blogger',
  'executor',
  'distiller',
  'bookkeeper',
  'fast',
  'deep',
]

const roleNames = () => identity.requiredNames.filter((name) => !name.endsWith('-bookkeeper'))

test('WHAT[PID-001] catalog_has_exactly_ten_canonical_roles_and_two_tiers', () => {
  assertJsData(identity.allRoleLabels, 'allRoleLabels')
  assert.deepEqual([...identity.allRoleLabels].sort(), [...EXPECTED_ROLES].sort())
  assert.equal(identity.allRoleLabels.length, 10)
  assert.equal(identity.allPublicRoleLabels.length + identity.allInternalRoleLabels.length, 10)
  assert.deepEqual(
    [...identity.allPublicRoleLabels, ...identity.allInternalRoleLabels].sort(),
    [...EXPECTED_ROLES].sort(),
  )
  assert.deepEqual(identity.peerTierLabel('Fast'), 'Deep')
  assert.deepEqual(identity.peerTierLabel('Deep'), 'Fast')
  assert.equal(identity.peerTierLabel('unknown'), '')
})

test('WHAT[PID-001] required_names_are_exactly_ten_roles_times_two_tiers', () => {
  assertJsData(identity.requiredNames, 'requiredNames')
  assert.equal(identity.requiredNames.length, 22)
  assert.equal(new Set(identity.requiredNames).size, 22)

  const names = roleNames()
  assert.equal(names.length, 20)
  const byRole = new Map()
  for (const name of names) {
    assert.match(name, /^(fast|deep)-[a-z]+$/)
    const role = name.slice(name.indexOf('-') + 1)
    byRole.set(role, (byRole.get(role) ?? 0) + 1)
    assert.equal(identity.isManagedName(name), true)
  }
  assert.equal(byRole.size, 10)
  for (const count of byRole.values()) assert.equal(count, 2)

  const derived = []
  for (const tier of ['fast', 'deep']) {
    for (const role of EXPECTED_ROLES) derived.push(identity.nameOf(tier, role))
  }
  assert.deepEqual(new Set(names), new Set(derived))
})

test('WHAT[PID-009] bookkeeper_pair_has_machine_identity_and_peer_but_no_public_role', () => {
  const bookkeepers = identity.requiredNames.filter((name) => name.endsWith('-bookkeeper'))
  assert.deepEqual(new Set(bookkeepers), new Set(['fast-bookkeeper', 'deep-bookkeeper']))
  assert.equal(identity.allRoleLabels.includes('bookkeeper'), false)
  for (const name of bookkeepers) {
    const peer = identity.peerName(name)
    assert.equal(bookkeepers.includes(peer), true)
    assert.equal(identity.peerName(peer), name)
    assert.equal(identity.isManagedName(name), true)
  }
})

test('WHAT[PID-007] peer_is_same_role_opposite_tier_and_symmetric', () => {
  const names = new Set(identity.requiredNames)
  for (const name of names) {
    const peer = identity.peerName(name)
    assert.equal(names.has(peer), true, `peer '${peer}' must be required`)
    assert.equal(identity.peerName(peer), name)
    const expected = name.startsWith('fast-')
      ? `deep-${name.slice('fast-'.length)}`
      : `fast-${name.slice('deep-'.length)}`
    assert.equal(peer, expected)
  }
})

test('WHAT[PID-002] all_legacy_bare_names_are_rejected', () => {
  assert.deepEqual(new Set(identity.legacyNames), new Set(EXPECTED_LEGACY))
  for (const bare of EXPECTED_LEGACY) {
    assert.equal(identity.isLegacyName(bare), true, `'${bare}' must be legacy`)
    assert.equal(identity.isManagedName(bare), false, `'${bare}' must not parse as managed`)
  }
  for (const shape of ['reviewer-fast', 'fast_reviewer']) {
    assert.equal(identity.isLegacyName(shape), true)
    assert.equal(identity.isManagedName(shape), false)
  }
  for (const name of identity.requiredNames) assert.equal(identity.isLegacyName(name), false)
})

test('WHAT[PID-002] rejection_prose_is_version_agnostic', () => {
  const supported = identity.formatLegacyNameNotSupported('coder')
  const inConfig = identity.formatLegacyNameInConfig('coder')
  for (const text of [supported, inConfig]) {
    assert.doesNotMatch(text, /0\.5\.\d/)
    assert.doesNotMatch(text, /Wanxiangshu\s+0\.5\.0/)
    assert.match(text, /Managed agents require explicit fast-\/deep- names\./)
  }
  assert.equal(
    supported,
    "Legacy agent name 'coder' is not supported. Managed agents require explicit fast-/deep- names.",
  )
  assert.equal(
    inConfig,
    "Legacy agent name 'coder' is present in opencode.json. Managed agents require explicit fast-/deep- names.",
  )
})
