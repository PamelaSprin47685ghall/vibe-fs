import assert from 'node:assert/strict'
import test from 'node:test'
import { createAccessTracker, recordSubstantiveAccess } from '../../../dist/Repository/Knowledge/Casebook/Surface.js'

test('WHAT[repository-programming-026] case capture ignores discovery and records explicit reads', () => {
  const tracker = createAccessTracker()
  recordSubstantiveAccess(tracker, 'grep', { path: 'scan-only.fs' }, true)
  recordSubstantiveAccess(tracker, 'glob', { path: 'enumerated-only.fs' }, true)
  recordSubstantiveAccess(tracker, 'read', { path: 'read.fs' }, false)
  recordSubstantiveAccess(tracker, 'read', { path: 'read.fs' }, false)
  assert.deepEqual(tracker.getRelatedPaths(), ['read.fs'])
})

test('WHAT[repository-programming-026] case capture records mutations only after their committed outcome', () => {
  for (const tool of ['write', 'create', 'edit', 'rewrite', 'rm']) {
    const tracker = createAccessTracker()
    recordSubstantiveAccess(tracker, tool, { path: 'changed.fs' }, false)
    assert.deepEqual(tracker.getRelatedPaths(), [], tool)
    recordSubstantiveAccess(tracker, tool, { path: 'changed.fs' }, true)
    assert.deepEqual(tracker.getRelatedPaths(), ['changed.fs'], tool)
  }
  const tracker = createAccessTracker()
  const args = { source: 'before.fs', destination: 'after.fs' }
  recordSubstantiveAccess(tracker, 'mv', args, false)
  assert.deepEqual(tracker.getRelatedPaths(), [])
  recordSubstantiveAccess(tracker, 'mv', args, true)
  assert.deepEqual(tracker.getRelatedPaths().sort(), ['after.fs', 'before.fs'])
})

test.todo('WHAT[repository-programming-026] actual program reads and commit outcomes feed case access without copying conflict snapshots')
