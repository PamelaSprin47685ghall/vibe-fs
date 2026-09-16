import assert from 'node:assert/strict'
import test from 'node:test'
import * as assoc from '../../../dist/Execution/Session/AssociationSurface.js'
import * as persona from '../../../dist/Participant/Persona/Surface.js'
import * as roles from '../../../dist/Foundation/RolesSurface.js'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'

// SESSION-ONTOLOGY proof — ExecutionClass × Ownership derived views.


const linked = assoc.link({ main: 'ses_main', blogger: 'ses_blogger' }, assoc.empty)
assert.equal(linked.ok, true, linked.message)
const state = linked.value

// SESSION-ONTOLOGY-001/002: durable links derive orthogonal, exhaustive cells.

// SESSION-ONTOLOGY proof — SyncDelegate attachment and ownership vocabulary.


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
      participant: resolved.identity.name,
      role: resolved.identity.role,
      persona: resolved.identity.persona,
      personaCatalogVersion: resolved.identity.catalogVersion,
      origin: resolved.identity.origin,
    },
  }
}
const syncDelegateRoles = ['Inspector', 'Coder']

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

test('WHAT[SESSION-ONTOLOGY-003] HOST_008_delegate_role_maps_to_attachment', () => {
  assert.equal(assoc.dedicatedAttachment('Inspector'), 'SyncInspector')
  assert.equal(assoc.dedicatedAttachment('Coder'), 'SyncCoder')
})
