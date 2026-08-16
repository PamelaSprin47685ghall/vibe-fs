// SESSION-ONTOLOGY proof — ExecutionClass × Ownership derived views.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as assoc from '../../../dist/Execution/Session/AssociationSurface.js'
import * as persona from '../../../dist/Participant/Persona/Surface.js'

const linked = assoc.link({ main: 'ses_main', blogger: 'ses_blogger' }, assoc.empty)
assert.equal(linked.ok, true, linked.message)
const state = linked.value

// SESSION-ONTOLOGY-001/002: durable links derive orthogonal, exhaustive cells.
test('WHAT[SESSION-ONTOLOGY-007] HOST_008_durable_link_derives_work_and_leaf_cells', () => {
  assert.deepEqual(assoc.classify('ses_main', state), {
    executionClass: 'Work',
    ownership: { kind: 'Root', owner: null, attachment: null, transactionId: null },
  })
  assert.deepEqual(assoc.classify('ses_blogger', state), {
    executionClass: 'InternalLeaf',
    ownership: { kind: 'Attached', owner: 'ses_main', attachment: 'Companion', transactionId: null },
  })
})

test('WHAT[SESSION-ONTOLOGY-004] HOST_008_companion_is_internal_leaf_attached', () => {
  const view = assoc.classify('ses_blogger', state)
  assert.equal(view.executionClass, 'InternalLeaf')
  assert.equal(view.ownership.kind, 'Attached')
  assert.equal(view.ownership.owner, 'ses_main')
  assert.equal(view.ownership.attachment, 'Companion')
})

test('WHAT[SESSION-ONTOLOGY-002] HOST_008_attached_carries_one_owner_and_one_kind', () => {
  const view = assoc.classify('ses_blogger', state)
  assert.equal(view.ownership.kind, 'Attached')
  assert.equal(typeof view.ownership.owner, 'string')
  assert.equal(view.ownership.attachment, 'Companion')
})

test('WHAT[SESSION-ONTOLOGY-003] EXEC_026_dedicated_sync_roles_are_work_plus_attached', () => {
  assert.equal(assoc.dedicatedExecutionClass, 'Work')
  assert.deepEqual(assoc.dedicatedOwnership('ses_owner', 'Inspector'), {
    kind: 'Attached', owner: 'ses_owner', attachment: 'SyncInspector', transactionId: null,
  })
  assert.deepEqual(assoc.dedicatedOwnership('ses_owner', 'Coder'), {
    kind: 'Attached', owner: 'ses_owner', attachment: 'SyncCoder', transactionId: null,
  })
  assert.equal(assoc.dedicatedAttachment('Inspector'), 'SyncInspector')
  assert.equal(assoc.dedicatedAttachment('Coder'), 'SyncCoder')
})

test('WHAT[SESSION-ONTOLOGY-004] HOST_008_strength_replica_is_internal_leaf_attached', () => {
  assert.equal(assoc.strengthExecutionClass, 'InternalLeaf')
  assert.deepEqual(assoc.strengthOwnership('ses_owner'), {
    kind: 'Attached', owner: 'ses_owner', attachment: 'StrengthReplica', transactionId: null,
  })
})

test('WHAT[SESSION-ONTOLOGY-011] HOST_008_strength_replica_is_not_a_satellite_kind', () => {
  assert.equal(assoc.isStrengthReplicaAttachment('StrengthReplica'), true)
  for (const kind of ['Companion', 'SyncInspector', 'SyncCoder', 'Bookkeeper']) {
    assert.equal(assoc.isStrengthReplicaAttachment(kind), false)
  }
  assert.deepEqual(assoc.satelliteKinds, ['Companion'])
})

test('WHAT[SESSION-ONTOLOGY-002] HOST_008_root_and_attached_helpers_are_plain_views', () => {
  assert.deepEqual(assoc.ownershipRoot, {
    kind: 'Root', owner: null, attachment: null, transactionId: null,
  })
  assert.deepEqual(assoc.ownershipAttached('ses_o', 'SyncCoder'), {
    kind: 'Attached', owner: 'ses_o', attachment: 'SyncCoder', transactionId: null,
  })
})

test('WHAT[SESSION-ONTOLOGY-001] HOST_008_execution_class_predicates_distinguish_work_and_leaf', () => {
  assert.deepEqual(assoc.executionClass('Work'), { name: 'Work', isWork: true, isInternalLeaf: false })
  assert.deepEqual(assoc.executionClass('InternalLeaf'), { name: 'InternalLeaf', isWork: false, isInternalLeaf: true })
})

test('WHAT[SESSION-ONTOLOGY-012] HOST_008_bookkeeper_attachment_carries_transaction_id', () => {
  assert.deepEqual(assoc.bookkeeperAttachment('tx-42'), { name: 'Bookkeeper', transactionId: 'tx-42' })
})

test('WHAT[SESSION-ONTOLOGY-013] HOST_008_canonical_role_label_is_catalog_stable', () => {
  for (const role of ['Manager', 'Coder', 'Orchestrator']) {
    assert.equal(persona.roleName(role), role.toLowerCase())
  }
  assert.equal(persona.roleName('renamed-case'), '')
})
