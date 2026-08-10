// tests/unit/kernel/sync-delegate.test.mjs — EXEC-026 / HOST-008 SyncDelegate helpers.
//
// Pure vocabulary only: role→attachment, owner tier passthrough, wire agent names,
// ReuseScopeId / DedicatedDelegateKey identity, and SessionOwnership helpers.
// SyncDelegateRuntime is out of scope.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  attachmentKind,
  caseOf,
  dedicatedDelegateKey,
  idValue,
  payloadOf,
  reuseScopeId,
  roles,
  sessionExecutionClass,
  sessionId,
  sessionOwnership,
  syncDelegate,
} from '../support/domain.mjs'

const ROLES = ['Inspector', 'Coder']
const TIERS = ['Fast', 'Deep']

const EXPECTED_ATTACHMENT = {
  Inspector: 'SyncInspector',
  Coder: 'SyncCoder',
}

const EXPECTED_AGENT_NAME = {
  Inspector: { Fast: 'fast-inspector', Deep: 'deep-inspector' },
  Coder: { Fast: 'fast-coder', Deep: 'deep-coder' },
}

// ── SyncDelegate.delegateRoleToAttachment ────────────────────────────────────

test('HOST_008_delegateRoleToAttachment_maps_inspector_and_coder', () => {
  for (const roleName of ROLES) {
    const attachment = syncDelegate.delegateRoleToAttachment(syncDelegate.role(roleName))
    assert.equal(caseOf(attachment), EXPECTED_ATTACHMENT[roleName])
  }
})

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
  for (const roleName of ROLES) {
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

// ── SessionOwnership helpers ─────────────────────────────────────────────────

test('HOST_008_SessionOwnership_tryOwner_and_attachmentKind', () => {
  const owner = sessionId('ses_owner')
  const root = sessionOwnership.root()
  const attached = sessionOwnership.attached(owner, attachmentKind.syncInspector())

  assert.equal(caseOf(root), 'Root')
  assert.equal(sessionOwnership.tryOwner(root), undefined)
  assert.equal(sessionOwnership.attachmentKind(root), undefined)

  assert.equal(caseOf(attached), 'Attached')
  assert.equal(idValue.session(sessionOwnership.tryOwner(attached)), 'ses_owner')
  assert.equal(caseOf(sessionOwnership.attachmentKind(attached)), 'SyncInspector')

  const coderAttached = sessionOwnership.attached(owner, attachmentKind.syncCoder())
  assert.equal(caseOf(sessionOwnership.attachmentKind(coderAttached)), 'SyncCoder')
})

test('HOST_008_SessionExecutionClass_predicates', () => {
  const work = sessionExecutionClass.of('Work')
  const leaf = sessionExecutionClass.of('InternalLeaf')

  assert.equal(sessionExecutionClass.isWork(work), true)
  assert.equal(sessionExecutionClass.isInternalLeaf(work), false)
  assert.equal(sessionExecutionClass.isWork(leaf), false)
  assert.equal(sessionExecutionClass.isInternalLeaf(leaf), true)
})

test('HOST_008_AttachmentKind_bookkeeper_carries_transaction_id', () => {
  const kind = attachmentKind.bookkeeper('tx-42')
  assert.equal(caseOf(kind), 'Bookkeeper')
  assert.equal(payloadOf(kind), 'tx-42')
})
