import assert from 'node:assert/strict'
import test from 'node:test'
import * as SessionsSurface from '../../../dist/OpenCode/Host/SessionsSurface.js'

test('WHAT[MANAGED-SESSION-003] HOST_015_abort_children_cascade_stays_keyed_on_family_root', () => {
  const parents = [{ child: 'child-1', parent: 'root' }, { child: 'child-2', parent: 'root' }]
  assert.deepEqual(SessionsSurface.physicalParents(parents, ['child-1', 'child-2']), ['root', 'root'])
  assert.equal(SessionsSurface.familyRoot(parents, 'child-1'), 'root')
  assert.equal(SessionsSurface.familyRoot(parents, 'child-2'), 'root')
})
