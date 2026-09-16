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

test('WHAT[SESSION-ONTOLOGY-002] HOST_008_attached_carries_one_owner_and_one_kind', () => {
  const view = assoc.classify('ses_blogger', state)
  assert.equal(view.ownership.kind, 'Attached')
  assert.equal(typeof view.ownership.owner, 'string')
  assert.equal(view.ownership.attachment, 'Companion')
})

test('WHAT[SESSION-ONTOLOGY-002] HOST_008_root_and_attached_helpers_are_plain_views', () => {
  assert.deepEqual(assoc.ownershipRoot, {
    kind: 'Root', owner: null, attachment: null, transactionId: null,
  })
  assert.deepEqual(assoc.ownershipAttached('ses_o', 'SyncCoder'), {
    kind: 'Attached', owner: 'ses_o', attachment: 'SyncCoder', transactionId: null,
  })
})

test('WHAT[SESSION-ONTOLOGY-002] HOST_008_attached_ownership_carries_owner_and_kind', () => {
  assert.deepEqual(assoc.ownershipAttached('ses_owner', 'SyncInspector'), {
    kind: 'Attached', owner: 'ses_owner', attachment: 'SyncInspector', transactionId: null,
  })
  assert.deepEqual(assoc.ownershipAttached('ses_owner', 'SyncCoder'), {
    kind: 'Attached', owner: 'ses_owner', attachment: 'SyncCoder', transactionId: null,
  })
})
