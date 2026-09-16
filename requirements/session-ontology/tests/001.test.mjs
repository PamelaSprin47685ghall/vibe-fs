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

test('WHAT[SESSION-ONTOLOGY-001] HOST_008_execution_class_predicates_distinguish_work_and_leaf', () => {
  assert.deepEqual(assoc.executionClass('Work'), { name: 'Work', isWork: true, isInternalLeaf: false })
  assert.deepEqual(assoc.executionClass('InternalLeaf'), { name: 'InternalLeaf', isWork: false, isInternalLeaf: true })
})

test('WHAT[SESSION-ONTOLOGY-001] HOST_008_execution_class_predicates_distinguish_work_and_leaf', () => {
  assert.equal(assoc.executionClass('Work').isWork, true)
  assert.equal(assoc.executionClass('Work').isInternalLeaf, false)
  assert.equal(assoc.executionClass('InternalLeaf').isWork, false)
  assert.equal(assoc.executionClass('InternalLeaf').isInternalLeaf, true)
})
