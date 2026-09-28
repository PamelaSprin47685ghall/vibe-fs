import assert from 'node:assert/strict'
import test from 'node:test'
import * as association from '../../../dist/Execution/Session/AssociationSurface.js'

test('WHAT[session-ontology-003] dedicated classification uses production role-to-attachment rules', () => {
  assert.equal(association.dedicatedExecutionClass, 'Work')
  for (const [role, attachment] of [['Inspector', 'SyncInspector'], ['Coder', 'SyncCoder'], ['Engineer', 'SyncInspector']]) {
    assert.deepEqual(association.dedicatedOwnership('owner', role), {
      kind: 'Attached', owner: 'owner', attachment, transactionId: null,
    })
    assert.equal(association.dedicatedAttachment(role), attachment)
  }
})
