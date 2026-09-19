import assert from 'node:assert/strict'
import test from 'node:test'
import * as assoc from '../../../dist/Execution/Session/AssociationSurface.js'
import * as persona from '../../../dist/Participant/Persona/Surface.js'

const linked = assoc.link({ main: 'ses_main', blogger: 'ses_blogger' }, assoc.empty)

assert.equal(linked.ok, true, linked.message)

const state = linked.value

test('WHAT[session-ontology-004] HOST_008_companion_is_internal_leaf_attached', () => {
  const view = assoc.classify('ses_blogger', state)
  assert.equal(view.executionClass, 'InternalLeaf')
  assert.equal(view.ownership.kind, 'Attached')
  assert.equal(view.ownership.owner, 'ses_main')
  assert.equal(view.ownership.attachment, 'Companion')
})

test('WHAT[session-ontology-004] HOST_008_strength_replica_is_internal_leaf_attached', () => {
  assert.equal(assoc.strengthExecutionClass, 'InternalLeaf')
  assert.deepEqual(assoc.strengthOwnership('ses_owner'), {
    kind: 'Attached', owner: 'ses_owner', attachment: 'StrengthReplica', transactionId: null,
  })
})
