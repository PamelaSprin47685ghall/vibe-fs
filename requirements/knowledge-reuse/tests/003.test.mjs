import assert from 'node:assert/strict'
import test from 'node:test'
import * as casebook from '../../../dist/Repository/Knowledge/Casebook/Surface.js'

test('WHAT[knowledge-reuse-003] production access recorder collects committed reads, mutations and both move endpoints only', () => {
  const tracker = casebook.createAccessTracker()
  for (const [tool, args] of [
    ['read', { path: 'read.txt' }], ['create', { path: 'new.txt' }],
    ['edit', { path: 'edited.txt' }], ['rm', { path: 'gone.txt' }],
    ['mv', { source: 'old.txt', destination: 'moved.txt' }], ['read', { path: 'read.txt' }],
  ]) casebook.recordSubstantiveAccess(tracker, tool, args, true)
  for (const tool of ['grep', 'glob', 'ls', 'symbol', 'executor']) {
    casebook.recordSubstantiveAccess(tracker, tool, { path: 'mention.txt', command: 'cat mention.txt' }, true)
  }
  assert.deepEqual(tracker.getRelatedPaths().sort(), ['edited.txt', 'gone.txt', 'moved.txt', 'new.txt', 'old.txt', 'read.txt'])
})

test('WHAT[knowledge-reuse-003] rejected mutations add no paths and do not erase an earlier successful read', () => {
  const tracker = casebook.createAccessTracker()
  casebook.recordSubstantiveAccess(tracker, 'read', { path: 'read.txt' }, true)
  for (const tool of ['write', 'create', 'edit', 'rm', 'delete']) {
    casebook.recordSubstantiveAccess(tracker, tool, { path: 'failed.txt' }, false)
  }
  casebook.recordSubstantiveAccess(tracker, 'mv', { source: 'from.txt', destination: 'to.txt' }, false)
  assert.deepEqual(tracker.getRelatedPaths(), ['read.txt'])
})

test.todo('WHAT[knowledge-reuse-003] GAP-160: actual repository tools collect every successful whole-file dependency, including partial reads and failure after prior reads')
