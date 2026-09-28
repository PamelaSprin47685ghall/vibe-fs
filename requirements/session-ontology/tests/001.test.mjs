import assert from 'node:assert/strict'
import test from 'node:test'
import * as association from '../../../dist/Execution/Session/AssociationSurface.js'

test('WHAT[session-ontology-001] execution predicates distinguish Work from InternalLeaf', () => {
  assert.deepEqual(association.executionClass('Work'), { name: 'Work', isWork: true, isInternalLeaf: false })
  assert.deepEqual(association.executionClass('InternalLeaf'), { name: 'InternalLeaf', isWork: false, isInternalLeaf: true })
})
