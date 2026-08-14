// requirements/participant-identity/tests/catalog.test.mjs — AGENT-001/002/003/004
// direct catalog tests (C5), moved from tests/unit/agent/.
//
// ManagedAgentCatalog is the sole identity directory (AGENT-001…004):
//   - 10 canonical roles × 2 tiers → exactly 20 required names (AGENT-002)
//   - peer = same role, opposite tier, symmetric for every name (AGENT-003)
//   - legacy bare names rejected, version-agnostic prose (AGENT-004)
//
// These are layer-1 tests against the published build: the Fable names are
// absorbed by the facade (VERIFY-008), so a renamed member fails at load time.

import assert from 'node:assert/strict'
import test from 'node:test'
import { authority, caseOf, managedAgentCatalog, roles } from '../../../tests/unit/support/domain.mjs'

const TIER_NAMES = ['Fast', 'Deep']

const EXPECTED_ROLES = [
  'Manager',
  'Orchestrator',
  'Coder',
  'Inspector',
  'Browser',
  'Inquiry',
  'Reviewer',
  'DevOps',
  'Distiller',
  'Blogger',
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

const PROSE = 'Managed agents require explicit fast-/deep- names.'

/** Split a managed name into (AgentTier, Role) through the catalog parsers. */
const tierAndRoleOf = (name) => {
  const [wireTier, ...rest] = name.split('-')
  const tier = managedAgentCatalog.tryParseTier(wireTier)
  const role = managedAgentCatalog.tryParseRole(rest.join('-'))
  assert.ok(tier !== undefined && role !== undefined, `catalog must parse its own name '${name}'`)
  return { tier, role }
}

// ── AGENT-001: Canonical Role and Agent Tier ─────────────────────────────────

test('AGENT_001_catalog_has_exactly_ten_canonical_roles_and_two_tiers', () => {
  const all = managedAgentCatalog.allRoles()
  assert.equal(all.length, 10)
  assert.deepEqual(new Set(all), new Set(EXPECTED_ROLES))

  // public + internal partition covers all 10 without overlap
  const publicRoles = managedAgentCatalog.allPublicRoles()
  const internalRoles = managedAgentCatalog.allInternalRoles()
  assert.equal(publicRoles.length + internalRoles.length, 10)
  for (const role of publicRoles) assert.equal(internalRoles.includes(role), false)

  // every canonical role round-trips label → parse → label
  for (const role of EXPECTED_ROLES) {
    const label = managedAgentCatalog.roleLabel(roles.of(role))
    assert.equal(label, role.toLowerCase())
    assert.equal(managedAgentCatalog.roleLabel(managedAgentCatalog.tryParseRole(label)), label)
  }

  // both tiers, both spellings, peer flips the tier
  for (const tierName of TIER_NAMES) {
    const tier = roles.tier(tierName)
    assert.equal(managedAgentCatalog.tierLabel(tier), tierName)
    assert.equal(managedAgentCatalog.wireTierLabel(tier), tierName.toLowerCase())
    assert.equal(caseOf(managedAgentCatalog.peerTier(tier)), tierName === 'Fast' ? 'Deep' : 'Fast')
  }
})

// ── AGENT-002: the 22 required agents ────────────────────────────────────────

test('AGENT_002_required_names_are_exactly_ten_roles_times_two_tiers_plus_bookkeeper', () => {
  const names = managedAgentCatalog.requiredNames()
  assert.equal(names.length, 22)
  assert.equal(new Set(names).size, 22)

  const roleNames = names.filter((n) => !n.endsWith('-bookkeeper'))
  const bookkeeperNames = names.filter((n) => n.endsWith('-bookkeeper'))
  assert.equal(roleNames.length, 20)
  assert.deepEqual(new Set(bookkeeperNames), new Set(['fast-bookkeeper', 'deep-bookkeeper']))

  // two names per role label (one fast, one deep)
  const byRole = new Map()
  for (const name of roleNames) {
    assert.match(name, /^(fast|deep)-[a-z]+$/)
    const role = name.slice(name.indexOf('-') + 1)
    byRole.set(role, (byRole.get(role) ?? 0) + 1)
  }
  assert.equal(byRole.size, 10)
  for (const [role, count] of byRole) {
    assert.equal(count, 2, `role '${role}' must have fast- and deep- variants`)
  }

  // every Role-based required name is a valid managed agent at the authority boundary
  for (const name of roleNames) {
    const parsed = authority.parseAgentName(name)
    assert.equal(parsed.ok, true, `'${name}' must parse as a managed agent`)
  }

  // Bookkeeper pair is catalog-only (no Role.Bookkeeper); peer still exists
  for (const name of bookkeeperNames) {
    const peer = managedAgentCatalog.bookkeeperPeerName(name)
    assert.ok(peer, `'${name}' must have a bookkeeper peer`)
    assert.equal(bookkeeperNames.includes(peer), true)
    assert.equal(managedAgentCatalog.bookkeeperPeerName(peer), name)
  }

  // Role names are catalog formulas, not a separate table
  const derived = []
  for (const tierName of TIER_NAMES) {
    for (const role of managedAgentCatalog.allRoles()) {
      derived.push(managedAgentCatalog.nameOf(roles.tier(tierName), roles.of(role)))
    }
  }
  for (const tierName of TIER_NAMES) {
    derived.push(managedAgentCatalog.bookkeeperNameOf(roles.tier(tierName)))
  }
  assert.deepEqual(new Set(names), new Set(derived))
})

// ── AGENT-003: Peer computation ──────────────────────────────────────────────

test('AGENT_003_peer_is_same_role_opposite_tier_and_symmetric', () => {
  const names = managedAgentCatalog.requiredNames()
  const nameSet = new Set(names)
  const peerOf = (name) => {
    if (managedAgentCatalog.isBookkeeperName(name)) {
      return managedAgentCatalog.bookkeeperPeerName(name)
    }
    const { tier, role } = tierAndRoleOf(name)
    return managedAgentCatalog.peerNameOf(tier, role)
  }

  for (const name of names) {
    const peer = peerOf(name)

    // AGENT-003: the peer must exist among the required names (proved at startup)
    assert.equal(nameSet.has(peer), true, `peer '${peer}' of '${name}' must be a required name`)

    // peer is the same role with the opposite tier
    const expected = name.startsWith('fast-')
      ? `deep-${name.slice('fast-'.length)}`
      : `fast-${name.slice('deep-'.length)}`
    assert.equal(peer, expected)

    // symmetry: peer(peer(x)) = x
    assert.equal(peerOf(peer), name)
  }
})

// ── AGENT-004: legacy names are refused ──────────────────────────────────────

test('AGENT_004_all_legacy_bare_names_are_rejected', () => {
  const legacy = managedAgentCatalog.legacyAgentNames()
  assert.deepEqual(new Set(legacy), new Set(EXPECTED_LEGACY))

  for (const bare of EXPECTED_LEGACY) {
    assert.equal(managedAgentCatalog.isLegacyAgentName(bare), true, `'${bare}' must be a legacy name`)
    const parsed = authority.parseAgentName(bare)
    assert.equal(parsed.ok, false, `'${bare}' must not parse`)
    assert.equal(caseOf(parsed.error), 'LegacyAgentName')
  }

  // forbidden shapes, not just the exact list (AGENT-004: no alias, no autocomplete)
  for (const shape of ['reviewer-fast', 'fast_reviewer']) {
    assert.equal(managedAgentCatalog.isLegacyAgentName(shape), true, `'${shape}' must be a legacy shape`)
    const parsed = authority.parseAgentName(shape)
    assert.equal(parsed.ok, false, `'${shape}' must not parse`)
    assert.equal(caseOf(parsed.error), 'LegacyAgentName')
  }

  // a managed name is never a legacy name
  for (const name of managedAgentCatalog.requiredNames()) {
    assert.equal(managedAgentCatalog.isLegacyAgentName(name), false, `'${name}' must not be legacy`)
  }
})

test('AGENT_004_rejection_prose_is_version_agnostic', () => {
  const supported = managedAgentCatalog.formatLegacyNameNotSupported('coder')
  const inConfig = managedAgentCatalog.formatLegacyNameInConfig('coder')

  for (const text of [supported, inConfig]) {
    assert.equal(/0\.5\.\d/.test(text), false, 'no version marker in rejection prose')
    assert.equal(/Wanxiangshu\s+0\.5\.0/.test(text), false)
    assert.equal(text.includes(PROSE), true)
  }

  assert.equal(
    supported,
    "Legacy agent name 'coder' is not supported. Managed agents require explicit fast-/deep- names.",
  )
  assert.equal(
    inConfig,
    "Legacy agent name 'coder' is present in opencode.json. Managed agents require explicit fast-/deep- names.",
  )

  // the authority path surfaces the same typed rejection (single emission point)
  const parsed = authority.parseAgentName('coder')
  assert.equal(parsed.ok, false)
  assert.equal(caseOf(parsed.error), 'LegacyAgentName')
})
