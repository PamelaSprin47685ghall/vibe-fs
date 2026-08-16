// SESSION-ONTOLOGY proof — physical Host children flatten to the family root.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as sessions from '../../../dist/OpenCode/Host/SessionsSurface.js'

test('WHAT[SESSION-ONTOLOGY-006] HOST_015_child_of_child_is_physically_parented_to_family_root', () => {
  const parents = [{ child: 'child-1', parent: 'root' }, { child: 'child-2', parent: 'root' }]
  assert.deepEqual(sessions.physicalParents(parents, ['root', 'child-1', 'child-2']), ['root', 'root', 'root'])
  assert.equal(sessions.familyRoot(parents, 'child-1'), 'root')
  assert.equal(sessions.familyRoot(parents, 'child-2'), 'root')
  assert.equal(sessions.familyRoot(parents, 'root'), 'root')
})

test('WHAT[SESSION-ONTOLOGY-006] HOST_015_family_root_resolves_through_restored_parents', () => {
  const restored = [{ child: 'devops', parent: 'manager' }, { child: 'manager', parent: 'root' }]
  assert.equal(sessions.familyRoot(restored, 'devops'), 'root')
  assert.deepEqual(sessions.physicalParents(restored, ['devops']), ['root'])
})
