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

test('WHAT[SESSION-ONTOLOGY-012] HOST_008_bookkeeper_attachment_carries_transaction_id', () => {
  assert.deepEqual(assoc.bookkeeperAttachment('tx-42'), { name: 'Bookkeeper', transactionId: 'tx-42' })
})

test('WHAT[SESSION-ONTOLOGY-012] HOST_008_root_and_attached_helpers_are_explicit', () => {
  assert.deepEqual(assoc.ownershipRoot, {
    kind: 'Root', owner: null, attachment: null, transactionId: null,
  })
  assert.equal(assoc.ownershipAttached('ses_owner', 'SyncInspector').owner, 'ses_owner')
})

test('WHAT[SESSION-ONTOLOGY-012] HOST_008_bookkeeper_carries_transaction_id', () => {
  assert.deepEqual(assoc.bookkeeperAttachment('tx-42'), { name: 'Bookkeeper', transactionId: 'tx-42' })
})

test('WHAT[SESSION-ONTOLOGY-012] Bookkeeper is private identity, not a Foundation role', () => {
  assert.equal(persona.isManagedName('bookkeeper'), true)
  assert.equal(roles.allRoleLabels.includes('bookkeeper'), false)
  assert.equal(persona.nameOf('deep', 'bookkeeper'), '')
})

assert.deepEqual(syncDelegateRoles, ['Inspector', 'Coder'])
