// Split from tests/unit/kernel/sync-delegate.test.mjs (cutover Wave 2a); owner: session-ontology
//
// HOST-008 SessionOwnership helpers — session-ontology half (SESSION-ONTOLOGY-003/012):
//   role→attachment, SessionOwnership tryOwner/attachmentKind, SessionExecutionClass
//   predicates, Bookkeeper transaction id. The EXEC-026 tier/agent-name vocabulary
//   moved to delegation with the rest of the file.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  attachmentKind,
  caseOf,
  idValue,
  payloadOf,
  sessionExecutionClass,
  sessionId,
  sessionOwnership,
  syncDelegate,
} from '../../verification-system/tests/support/domain.mjs'

const ROLES = ['Inspector', 'Coder']

const EXPECTED_ATTACHMENT = {
  Inspector: 'SyncInspector',
  Coder: 'SyncCoder',
}

// ── SyncDelegate.delegateRoleToAttachment ────────────────────────────────────

test('WHAT[SESSION-ONTOLOGY-003] HOST_008_delegateRoleToAttachment_maps_inspector_and_coder', () => {
  for (const roleName of ROLES) {
    const attachment = syncDelegate.delegateRoleToAttachment(syncDelegate.role(roleName))
    assert.equal(caseOf(attachment), EXPECTED_ATTACHMENT[roleName])
  }
})

// ── SessionOwnership helpers ─────────────────────────────────────────────────

test('WHAT[SESSION-ONTOLOGY-002] HOST_008_SessionOwnership_attached_carries_owner_and_kind', () => {
  const owner = sessionId('ses_owner')
  const attached = sessionOwnership.attached(owner, attachmentKind.syncInspector())

  assert.equal(attached.name, 'Attached')
  assert.equal(idValue.session(sessionOwnership.tryOwner(attached)), 'ses_owner')
  assert.equal(sessionOwnership.attachmentKind(attached).name, 'SyncInspector')

  const coderAttached = sessionOwnership.attached(owner, attachmentKind.syncCoder())
  assert.equal(sessionOwnership.attachmentKind(coderAttached).name, 'SyncCoder')
})

test('WHAT[SESSION-ONTOLOGY-012] HOST_008_SessionOwnership_root_and_attached_helpers', () => {
  const owner = sessionId('ses_owner')
  const root = sessionOwnership.root()
  const attached = sessionOwnership.attached(owner, attachmentKind.syncInspector())

  assert.equal(caseOf(root), 'Root')
  assert.equal(sessionOwnership.tryOwner(root), undefined)
  assert.equal(sessionOwnership.attachmentKind(root), undefined)

  assert.equal(caseOf(attached), 'Attached')
  assert.equal(idValue.session(sessionOwnership.tryOwner(attached)), 'ses_owner')
  assert.equal(caseOf(sessionOwnership.attachmentKind(attached)), 'SyncInspector')
})

test('WHAT[SESSION-ONTOLOGY-001] HOST_008_SessionExecutionClass_predicates_distinguish_work_from_leaf', () => {
  const work = sessionExecutionClass.of('Work')
  const leaf = sessionExecutionClass.of('InternalLeaf')

  assert.equal(sessionExecutionClass.isWork(work), true)
  assert.equal(sessionExecutionClass.isInternalLeaf(work), false)
  assert.equal(sessionExecutionClass.isWork(leaf), false)
  assert.equal(sessionExecutionClass.isInternalLeaf(leaf), true)
})

test('WHAT[SESSION-ONTOLOGY-012] HOST_008_AttachmentKind_bookkeeper_carries_transaction_id', () => {
  const kind = attachmentKind.bookkeeper('tx-42')
  assert.equal(caseOf(kind), 'Bookkeeper')
  assert.equal(payloadOf(kind), 'tx-42')
})
