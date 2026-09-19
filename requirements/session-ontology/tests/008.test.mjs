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

test('WHAT[session-ontology-008] HOST_008_linking_records_both_directions', () => {
  const state = linked([{ main: 'ses_x', blogger: 'ses_y' }])
  assert.deepEqual(assoc.entry('ses_x', state), {
    kind: 'WorkSession',
    mainSessionId: null,
    satelliteKind: null,
    blogger: 'ses_y',
    parent: null,
  })
  assert.deepEqual(assoc.entry('ses_y', state), {
    kind: 'SatelliteSession',
    mainSessionId: 'ses_x',
    satelliteKind: 'Companion',
    blogger: null,
    parent: 'ses_x',
  })
  assert.deepEqual(assoc.ids(state), ['ses_x', 'ses_y'])
})
test('WHAT[session-ontology-008] COMPANION_002_companion_side_answers_isCompanion_immediately', () => {
  const state = linked([{ main: 'ses_x', blogger: 'ses_y' }])
  assert.equal(assoc.isCompanion('ses_y', state), true)
  assert.equal(assoc.isCompanion('ses_x', state), false)
  assert.equal(assoc.mainSessionOf('ses_y', state), 'ses_x')
  assert.equal(assoc.mainSessionOf('ses_x', state), null)
})
test('WHAT[session-ontology-008] COMPANION_002_companion_is_structurally_a_leaf', () => {
  const state = linked([{ main: 'ses_x', blogger: 'ses_y' }])
  assert.equal(assoc.bloggerOf('ses_y', state), null)
})
test('WHAT[session-ontology-008] COMPANION_002_companion_cannot_receive_a_companion', () => {
  const result = assoc.link({ main: 'ses_y', blogger: 'ses_z' }, linked([{ main: 'ses_x', blogger: 'ses_y' }]))
  assert.equal(result.ok, false)
  assert.equal(result.error, 'CompanionWouldRecurse')
  assert.match(result.message, /COMPANION-002/)
})
test('WHAT[session-ontology-008] COMPANION_003_relinking_same_pair_is_idempotent', () => {
  const once = linked([{ main: 'ses_x', blogger: 'ses_y' }])
  const twice = linked([{ main: 'ses_x', blogger: 'ses_y' }], once)
  assert.deepEqual(assoc.entry('ses_x', twice), assoc.entry('ses_x', once))
  assert.deepEqual(assoc.entry('ses_y', twice), assoc.entry('ses_y', once))
  assert.deepEqual(assoc.ids(twice), ['ses_x', 'ses_y'])
})
test('WHAT[session-ontology-008] COMPANION_002_second_companion_for_one_work_session_is_refused', () => {
  const result = assoc.link({ main: 'ses_x', blogger: 'ses_y2' }, linked([{ main: 'ses_x', blogger: 'ses_y1' }]))
  assert.equal(result.ok, false)
  assert.equal(result.error, 'AlreadyLinkedToOther')
  assert.match(result.message, /ses_y1/)
  assert.match(result.message, /ses_y2/)
})
test('WHAT[session-ontology-008] COMPANION_002_unlinking_does_not_disturb_another_pair', () => {
  const state = linked([
    { main: 'ses_x1', blogger: 'ses_y1' },
    { main: 'ses_x2', blogger: 'ses_y2' },
  ])
  const unlinked = assoc.unlink('ses_x1', state)
  assert.equal(assoc.bloggerOf('ses_x2', unlinked), 'ses_y2')
  assert.equal(assoc.isCompanion('ses_y2', unlinked), true)
  assert.deepEqual(assoc.ids(unlinked), ['ses_x1', 'ses_x2', 'ses_y2'])
})
}

