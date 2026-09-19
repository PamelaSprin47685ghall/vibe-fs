import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const assoc = await import("../../../dist/Execution/Session/AssociationSurface.js");

const linked = (pairs, start = assoc.empty) =>
  pairs.reduce((state, pair) => {
    const result = assoc.link(pair, state)
    assert.equal(result.ok, true, result.message)
    return result.value
  }, start)

test('WHAT[session-ontology-006] HOST_008_work_parent_is_recorded_when_supplied', () => {
  const state = linked([{ main: 'ses_child', blogger: 'ses_child_y', parent: 'ses_parent' }])
  assert.equal(assoc.entry('ses_child', state).parent, 'ses_parent')
  assert.equal(assoc.entry('ses_child_y', state).parent, 'ses_child')
})
test('WHAT[session-ontology-006] HOST_008_relink_without_parent_does_not_erase_known_parent', () => {
  const withParent = linked([{ main: 'ses_child', blogger: 'ses_y', parent: 'ses_parent' }])
  const relinked = linked([{ main: 'ses_child', blogger: 'ses_y' }], withParent)
  assert.equal(assoc.entry('ses_child', relinked).parent, 'ses_parent')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const sessions = await import("../../../dist/OpenCode/Host/SessionsSurface.js");


test('WHAT[session-ontology-006] HOST_015_child_of_child_is_physically_parented_to_family_root', () => {
  const parents = [{ child: 'child-1', parent: 'root' }, { child: 'child-2', parent: 'root' }]
  assert.deepEqual(sessions.physicalParents(parents, ['root', 'child-1', 'child-2']), ['root', 'root', 'root'])
  assert.equal(sessions.familyRoot(parents, 'child-1'), 'root')
  assert.equal(sessions.familyRoot(parents, 'child-2'), 'root')
  assert.equal(sessions.familyRoot(parents, 'root'), 'root')
})
test('WHAT[session-ontology-006] HOST_015_family_root_resolves_through_restored_parents', () => {
  const restored = [{ child: 'devops', parent: 'manager' }, { child: 'manager', parent: 'root' }]
  assert.equal(sessions.familyRoot(restored, 'devops'), 'root')
  assert.deepEqual(sessions.physicalParents(restored, ['devops']), ['root'])
})
}
