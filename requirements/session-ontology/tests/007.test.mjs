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
