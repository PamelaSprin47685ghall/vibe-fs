import assert from 'node:assert/strict'
import test from 'node:test'
import * as association from '../../../dist/Execution/Session/AssociationSurface.js'

test('WHAT[session-ontology-004] a linked Companion is an owned leaf with no Companion of its own', () => {
  const linked = association.link({ main: 'work', blogger: 'companion' }, association.empty)
  assert.equal(linked.ok, true, linked.message)
  assert.deepEqual(association.classify('companion', linked.value), {
    executionClass: 'InternalLeaf',
    ownership: { kind: 'Attached', owner: 'work', attachment: 'Companion', transactionId: null },
  })
  assert.equal(association.bloggerOf('companion', linked.value), null)
})

test('WHAT[session-ontology-004] production replica classification is InternalLeaf plus Attached', () => {
  assert.equal(association.strengthExecutionClass, 'InternalLeaf')
  assert.deepEqual(association.strengthOwnership('owner'), {
    kind: 'Attached', owner: 'owner', attachment: 'StrengthReplica', transactionId: null,
  })
})
