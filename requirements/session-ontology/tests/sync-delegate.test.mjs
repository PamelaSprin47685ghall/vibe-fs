// SESSION-ONTOLOGY proof — SyncDelegate attachment and ownership vocabulary.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as assoc from '../../../dist/Execution/Session/AssociationSurface.js'

const roles = ['Inspector', 'Coder']

test('WHAT[SESSION-ONTOLOGY-003] HOST_008_delegate_role_maps_to_attachment', () => {
  assert.equal(assoc.dedicatedAttachment('Inspector'), 'SyncInspector')
  assert.equal(assoc.dedicatedAttachment('Coder'), 'SyncCoder')
})

test('WHAT[SESSION-ONTOLOGY-002] HOST_008_attached_ownership_carries_owner_and_kind', () => {
  assert.deepEqual(assoc.ownershipAttached('ses_owner', 'SyncInspector'), {
    kind: 'Attached', owner: 'ses_owner', attachment: 'SyncInspector', transactionId: null,
  })
  assert.deepEqual(assoc.ownershipAttached('ses_owner', 'SyncCoder'), {
    kind: 'Attached', owner: 'ses_owner', attachment: 'SyncCoder', transactionId: null,
  })
})

test('WHAT[SESSION-ONTOLOGY-012] HOST_008_root_and_attached_helpers_are_explicit', () => {
  assert.deepEqual(assoc.ownershipRoot, {
    kind: 'Root', owner: null, attachment: null, transactionId: null,
  })
  assert.equal(assoc.ownershipAttached('ses_owner', 'SyncInspector').owner, 'ses_owner')
})

test('WHAT[SESSION-ONTOLOGY-001] HOST_008_execution_class_predicates_distinguish_work_and_leaf', () => {
  assert.equal(assoc.executionClass('Work').isWork, true)
  assert.equal(assoc.executionClass('Work').isInternalLeaf, false)
  assert.equal(assoc.executionClass('InternalLeaf').isWork, false)
  assert.equal(assoc.executionClass('InternalLeaf').isInternalLeaf, true)
})

test('WHAT[SESSION-ONTOLOGY-012] HOST_008_bookkeeper_carries_transaction_id', () => {
  assert.deepEqual(assoc.bookkeeperAttachment('tx-42'), { name: 'Bookkeeper', transactionId: 'tx-42' })
})

assert.deepEqual(roles, ['Inspector', 'Coder'])
