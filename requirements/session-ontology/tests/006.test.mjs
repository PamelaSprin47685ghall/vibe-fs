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

test.todo('WHAT[session-ontology-006] actual Host child creation must pass family root as physical parent; a Surface-local ancestry algorithm is not adapter evidence')
