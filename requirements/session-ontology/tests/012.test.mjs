import assert from 'node:assert/strict'
import test from 'node:test'
import * as association from '../../../dist/Execution/Session/AssociationSurface.js'

test('WHAT[session-ontology-012] Bookkeeper attachment preserves the exact transaction identity', () => {
  for (const transactionId of ['tx-42', '事务-7']) {
    assert.deepEqual(association.bookkeeperAttachment(transactionId), { name: 'Bookkeeper', transactionId })
  }
})
