import assert from 'node:assert/strict'
import test from 'node:test'
import * as log from '../../../dist/Persistence/LocalProcessEventLogSurface.js'

test('WHAT[DURABLE-EVENTS-017] DURABLE_EVENTS_004_017_local_append_has_zero_Git_object_tree_ref_dependencies', () => {
  const ops = log.countGitOperationsOnAppend()
  assert.equal(ops.blobCreates, 0)
  assert.equal(ops.treeWrites, 0)
  assert.equal(ops.refUpdates, 0)
})
