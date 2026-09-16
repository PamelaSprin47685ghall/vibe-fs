import assert from 'node:assert/strict'
import test from 'node:test'
import * as assoc from '../../../dist/Execution/Session/AssociationSurface.js'
import * as sessions from '../../../dist/OpenCode/Host/SessionsSurface.js'

// SESSION-ONTOLOGY proof — durable Work ↔ Companion association.


const linked = (pairs, start = assoc.empty) =>
  pairs.reduce((state, pair) => {
    const result = assoc.link(pair, state)
    assert.equal(result.ok, true, result.message)
    return result.value
  }, start)

// SESSION-ONTOLOGY-010: an unknown id is not structurally a Companion.

// SESSION-ONTOLOGY proof — physical Host children flatten to the family root.

test('WHAT[SESSION-ONTOLOGY-006] HOST_008_work_parent_is_recorded_when_supplied', () => {
  const state = linked([{ main: 'ses_child', blogger: 'ses_child_y', parent: 'ses_parent' }])
  assert.equal(assoc.entry('ses_child', state).parent, 'ses_parent')
  assert.equal(assoc.entry('ses_child_y', state).parent, 'ses_child')
})

test('WHAT[SESSION-ONTOLOGY-006] HOST_008_relink_without_parent_does_not_erase_known_parent', () => {
  const withParent = linked([{ main: 'ses_child', blogger: 'ses_y', parent: 'ses_parent' }])
  const relinked = linked([{ main: 'ses_child', blogger: 'ses_y' }], withParent)
  assert.equal(assoc.entry('ses_child', relinked).parent, 'ses_parent')
})

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
