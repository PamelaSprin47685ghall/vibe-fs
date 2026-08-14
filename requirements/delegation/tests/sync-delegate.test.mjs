// Split from tests/unit/kernel/sync-delegate.test.mjs (cutover Wave 2a); owner: delegation
//
// EXEC-026 SyncDelegate pure vocabulary — delegation half (DELEG-010):
//   owner tier passthrough, wire agent names, ReuseScopeId / DedicatedDelegateKey identity.
// The HOST-008 session-ownership helpers moved to session-ontology with the rest of
// the file; SyncDelegateRuntime stays out of scope (session/sync-delegate-runtime).

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  caseOf,
  dedicatedDelegateKey,
  reuseScopeId,
  roles,
  syncDelegate,
} from '../../verification-system/tests/support/domain.mjs'

const TIERS = ['Fast', 'Deep']

const EXPECTED_AGENT_NAME = {
  Inspector: { Fast: 'fast-inspector', Deep: 'deep-inspector' },
  Coder: { Fast: 'fast-coder', Deep: 'deep-coder' },
}

// ── SyncDelegate.tierForOwner ────────────────────────────────────────────────

test('EXEC_026_tierForOwner_is_identity_for_fast_and_deep', () => {
  for (const tierName of TIERS) {
    const ownerTier = roles.tier(tierName)
    const mapped = syncDelegate.tierForOwner(ownerTier)
    assert.equal(caseOf(mapped), tierName)
    assert.equal(mapped, ownerTier, 'tierForOwner must return the same AgentTier value')
  }
})

// ── SyncDelegate.agentNameFor ────────────────────────────────────────────────

test('EXEC_026_agentNameFor_covers_fast_deep_times_inspector_coder', () => {
  for (const roleName of ['Inspector', 'Coder']) {
    for (const tierName of TIERS) {
      const name = syncDelegate.agentNameFor(syncDelegate.role(roleName), roles.tier(tierName))
      assert.equal(name, EXPECTED_AGENT_NAME[roleName][tierName])
    }
  }
})

// ── ReuseScopeId / DedicatedDelegateKey ──────────────────────────────────────

test('EXEC_026_ReuseScopeId_create_value_and_equals', () => {
  const a = reuseScopeId.create('owner-reuse-scope')
  const b = reuseScopeId.create('owner-reuse-scope')
  const c = reuseScopeId.create('other-scope')

  assert.equal(reuseScopeId.value(a), 'owner-reuse-scope')
  assert.equal(reuseScopeId.equals(a, b), true)
  assert.equal(reuseScopeId.equals(a, c), false)
})

test('EXEC_026_DedicatedDelegateKey_binds_scope_and_role', () => {
  const scope = reuseScopeId.create('scope-1')
  const key = dedicatedDelegateKey.create(scope, syncDelegate.role('Inspector'))

  assert.equal(reuseScopeId.value(key.Scope), 'scope-1')
  assert.equal(caseOf(key.Role), 'Inspector')

  const coderKey = dedicatedDelegateKey.create(scope, syncDelegate.role('Coder'))
  assert.equal(caseOf(coderKey.Role), 'Coder')
  assert.equal(reuseScopeId.equals(key.Scope, coderKey.Scope), true)
})
