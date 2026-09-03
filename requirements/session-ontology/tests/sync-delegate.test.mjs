// SESSION-ONTOLOGY proof — SyncDelegate attachment and ownership vocabulary.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as assoc from '../../../dist/Execution/Session/AssociationSurface.js'
import * as roles from '../../../dist/Foundation/RolesSurface.js'
import * as persona from '../../../dist/Participant/Persona/Surface.js'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'

const H = (value) => `H(${value})`
const rootSelection = (agent) => {
  const resolved = persona.resolveParticipantIdentityAtRoot(agent)
  assert.equal(resolved.ok, true, resolved.ok ? '' : resolved.error)
  return {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: {
      selectedAgent: resolved.identity.name,
      peerAgent: resolved.identity.peer,
      canonicalRole: resolved.identity.role,
      selectedTier: resolved.identity.initialTier.toLowerCase(),
      persona: resolved.identity.persona,
      personaCatalogVersion: resolved.identity.catalogVersion,
      origin: resolved.identity.origin,
    },
  }
}
const syncDelegateRoles = ['Inspector', 'Coder']

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

test('WHAT[PID-008] SyncDelegate identity inherits its exact owner Persona and version', () => {
  const created = authority.createAuthorityRoot(
    H,
    'runtime-sync-lineage',
    'ses_sync_owner',
    'HumanRoot',
    'msg_sync_owner',
    rootSelection('manager'),
  )
  assert.equal(created.ok, true, created.ok ? '' : created.error)
  const owner = created.value
  const issued = authority.issueInheritedIdentitySeed('inspector', owner)
  assert.equal(issued.ok, true, issued.ok ? '' : issued.error)

  assert.deepEqual(
    {
      selectedAgent: issued.value.participantIdentity.selectedAgent,
      canonicalRole: issued.value.participantIdentity.canonicalRole,
      selectedTier: issued.value.participantIdentity.selectedTier,
      persona: issued.value.participantIdentity.persona,
      personaCatalogVersion: issued.value.participantIdentity.personaCatalogVersion,
      ownerSession: issued.value.ownerSession,
      ownerLogicalRun: issued.value.ownerLogicalRun,
      ownerAuthorityRoot: issued.value.ownerAuthorityRoot,
    },
    {
      selectedAgent: 'inspector',
      canonicalRole: 'inspector',
      selectedTier: 'deep',
      persona: owner.participantIdentity.persona,
      personaCatalogVersion: owner.participantIdentity.personaCatalogVersion,
      ownerSession: owner.session,
      ownerLogicalRun: owner.logicalRun,
      ownerAuthorityRoot: owner.authorityRoot,
    },
  )
})

test('WHAT[SESSION-ONTOLOGY-012] Bookkeeper is private identity, not a Foundation role', () => {
  assert.equal(persona.isManagedName('bookkeeper'), true)
  assert.equal(roles.allRoleLabels.includes('bookkeeper'), false)
  assert.equal(persona.nameOf('deep', 'bookkeeper'), '')
})

assert.deepEqual(syncDelegateRoles, ['Inspector', 'Coder'])
