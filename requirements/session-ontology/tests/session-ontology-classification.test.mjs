// session-ontology: HOST-008 orthogonal ExecutionClass × Ownership classification.
//
// The durable SessionAssociation stays on the ManagedSessionKind model; the
// orthogonal view (SessionOwnershipClassification) is derived, additive only.
// Dedicated SyncInspector/SyncCoder are Work+Attached via SyncDelegateAssociationHints;
// StrengthReplica is Universal InternalLeaf+Attached via StrengthReplicaAssociationHints
// and is NEVER a SatelliteKind case. The canonical durable role label comes from
// AgentRoleIdentity.roleName (ManagedAgentCatalog), not a DU-name ToString.

import assert from 'node:assert/strict'
import test from 'node:test'

import {
  attachmentKind,
  caseOf,
  sessionAssociation,
  sessionExecutionClass,
  sessionId,
  sessionOwnership,
  syncDelegate,
} from '../../verification-system/tests/support/domain.mjs'

const {
  SessionOwnershipClassification_executionClassOf: executionClassOf,
  SessionOwnershipClassification_classifyLegacy: classifyLegacy,
  SessionOwnershipClassification_tryClassify: tryClassify,
  SessionAssociationProjection_tryFind: tryFind,
  SyncDelegateAssociationHints_dedicatedExecutionClass: dedicatedExecutionClass,
  SyncDelegateAssociationHints_dedicatedOwnership: dedicatedOwnership,
  StrengthReplicaAssociationHints_executionClass: strengthExecutionClass,
  StrengthReplicaAssociationHints_ownership: strengthOwnership,
  StrengthReplicaAssociationHints_isStrengthReplicaAttachment: isStrengthReplicaAttachment,
} = await import('../../../dist/Execution/Session/Association.js')

const { roleName } = await import('../../../dist/Participant/Persona/RoleIdentity.js')
const { Role } = await import('../../../dist/Foundation/Roles.js')
const { AttachmentKind } = await import('../../../dist/Execution/Session/Ownership.js')

const linkedPair = () => {
  // sessionAssociation.link takes plain session-id strings (the facade wraps
  // them) and returns { ok, value } — the map lives in `.value`.
  const main = 'ses_main'
  const blogger = 'ses_blogger'
  const linked = sessionAssociation.link({ main, blogger }, sessionAssociation.empty)
  assert.equal(linked.ok, true, linked.ok ? '' : linked.error)
  return { main: sessionId(main), blogger: sessionId(blogger), current: linked.value }
}

// tryClassify returns the tuple (ExecutionClass * Ownership option) directly when
// found (Fable erases the outer Some), undefined when absent. The ownership
// option is likewise erased: Root is the SessionOwnership union itself.
const classify = (session, current) => {
  const view = tryClassify(session, current)
  assert.ok(view, 'must classify')
  return {
    executionClass: caseOf(view[0]),
    ownership: caseOf(view[1]),
    ownershipFields: view[1].fields,
  }
}

test('HOST_008_orthogonal_execution_class_and_ownership_are_derived_from_the_durable_link', () => {
  const { main, blogger, current } = linkedPair()

  const mainView = classify(main, current)
  assert.equal(mainView.executionClass, 'Work')
  assert.equal(mainView.ownership, 'Root')

  const bloggerView = classify(blogger, current)
  assert.equal(bloggerView.executionClass, 'InternalLeaf')
  assert.equal(bloggerView.ownership, 'Attached')
  // Attached(ownerSessionId, AttachmentKind): owner is the work session.
  assert.equal(bloggerView.ownershipFields[0].fields[0], 'ses_main')
  assert.equal(caseOf(bloggerView.ownershipFields[1]), 'Companion')
})

test('HOST_008_executionClassOf_maps_durable_kind_without_inventing_ownership', () => {
  const { main, blogger, current } = linkedPair()
  const mainEntry = tryFind(main, current)
  const bloggerEntry = tryFind(blogger, current)
  assert.ok(mainEntry)
  assert.ok(bloggerEntry)

  // WorkSession → Work; SatelliteSession(_, Companion) → InternalLeaf. Ownership
  // is NOT invented here: callers that know the SyncDelegate role use the hints.
  assert.equal(caseOf(executionClassOf(mainEntry.Kind)), 'Work')
  assert.equal(caseOf(executionClassOf(bloggerEntry.Kind)), 'InternalLeaf')

  // classifyLegacy is the additive single-entry projection: (class, ownership option).
  const [mainClass, mainOwnership] = classifyLegacy(mainEntry)
  assert.equal(caseOf(mainClass), 'Work')
  assert.equal(caseOf(mainOwnership), 'Root')
})

test('EXEC_026_dedicated_sync_inspector_and_coder_are_work_plus_attached', () => {
  assert.equal(caseOf(dedicatedExecutionClass), 'Work')

  const inspector = dedicatedOwnership(sessionId('ses_owner'), syncDelegate.role('Inspector'))
  assert.equal(caseOf(inspector), 'Attached')
  assert.equal(inspector.fields[0].fields[0], 'ses_owner')
  assert.equal(caseOf(inspector.fields[1]), 'SyncInspector')

  const coder = dedicatedOwnership(sessionId('ses_owner'), syncDelegate.role('Coder'))
  assert.equal(caseOf(coder.fields[1]), 'SyncCoder')

  // The pure mapping agrees: role → AttachmentKind.
  assert.equal(caseOf(syncDelegate.delegateRoleToAttachment(syncDelegate.role('Inspector'))), 'SyncInspector')
  assert.equal(caseOf(syncDelegate.delegateRoleToAttachment(syncDelegate.role('Coder'))), 'SyncCoder')
})

test('HOST_008_strength_replica_is_universal_internal_leaf_attachment_never_satellite_kind', () => {
  assert.equal(caseOf(strengthExecutionClass), 'InternalLeaf')

  const replica = strengthOwnership(sessionId('ses_owner'))
  assert.equal(caseOf(replica), 'Attached')
  assert.equal(replica.fields[0].fields[0], 'ses_owner')
  assert.equal(caseOf(replica.fields[1]), 'StrengthReplica')

  assert.equal(isStrengthReplicaAttachment(AttachmentKind.StrengthReplica), true)
  assert.equal(isStrengthReplicaAttachment(attachmentKind.companion()), false)
  assert.equal(isStrengthReplicaAttachment(attachmentKind.syncInspector()), false)
  assert.equal(isStrengthReplicaAttachment(attachmentKind.bookkeeper('tx-1')), false)
})

test('HOST_008_root_and_attached_helpers_agree_with_the_type_model', () => {
  const root = sessionOwnership.root()
  assert.equal(caseOf(root), 'Root')
  assert.equal(sessionOwnership.tryOwner(root), undefined)
  assert.equal(sessionOwnership.attachmentKind(root), undefined)

  const attached = sessionOwnership.attached(sessionId('ses_o'), attachmentKind.syncCoder())
  assert.equal(caseOf(attached), 'Attached')
  assert.equal(sessionOwnership.tryOwner(attached).fields[0], 'ses_o')
  assert.equal(caseOf(sessionOwnership.attachmentKind(attached)), 'SyncCoder')

  assert.equal(sessionExecutionClass.isWork(sessionExecutionClass.of('Work')), true)
  assert.equal(sessionExecutionClass.isInternalLeaf(sessionExecutionClass.of('Work')), false)
  assert.equal(sessionExecutionClass.isWork(sessionExecutionClass.of('InternalLeaf')), false)
  assert.equal(sessionExecutionClass.isInternalLeaf(sessionExecutionClass.of('InternalLeaf')), true)
})

test('HOST_008_canonical_durable_role_label_is_stable_via_catalog_not_du_name', () => {
  assert.equal(roleName(Role.Manager), 'manager')
  assert.equal(roleName(Role.Coder), 'coder')
  assert.equal(roleName(Role.Orchestrator), 'orchestrator')
  // A DU-case rename must not silently change the durable string: the label is
  // the catalog spelling, so an old journal keeps decoding.
  assert.equal(roleName(Role.Coder), 'coder')
})
