// SESSION-ONTOLOGY proof — durable Work ↔ Companion association.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as assoc from '../../../dist/Execution/Session/AssociationSurface.js'

const linked = (pairs, start = assoc.empty) =>
  pairs.reduce((state, pair) => {
    const result = assoc.link(pair, state)
    assert.equal(result.ok, true, result.message)
    return result.value
  }, start)

// SESSION-ONTOLOGY-010: an unknown id is not structurally a Companion.
test('WHAT[SESSION-ONTOLOGY-010] COMPANION_001_unknown_session_is_not_a_companion', () => {
  assert.equal(assoc.isCompanion('ses_unknown', assoc.empty), false)
  assert.equal(assoc.bloggerOf('ses_unknown', assoc.empty), null)
  assert.equal(assoc.entry('ses_unknown', assoc.empty), null)
})

test('WHAT[SESSION-ONTOLOGY-008] HOST_008_linking_records_both_directions', () => {
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

test('WHAT[SESSION-ONTOLOGY-008] COMPANION_002_companion_side_answers_isCompanion_immediately', () => {
  const state = linked([{ main: 'ses_x', blogger: 'ses_y' }])
  assert.equal(assoc.isCompanion('ses_y', state), true)
  assert.equal(assoc.isCompanion('ses_x', state), false)
  assert.equal(assoc.mainSessionOf('ses_y', state), 'ses_x')
  assert.equal(assoc.mainSessionOf('ses_x', state), null)
})

test('WHAT[SESSION-ONTOLOGY-008] COMPANION_002_companion_is_structurally_a_leaf', () => {
  const state = linked([{ main: 'ses_x', blogger: 'ses_y' }])
  assert.equal(assoc.bloggerOf('ses_y', state), null)
})

test('WHAT[SESSION-ONTOLOGY-008] COMPANION_002_companion_cannot_receive_a_companion', () => {
  const result = assoc.link({ main: 'ses_y', blogger: 'ses_z' }, linked([{ main: 'ses_x', blogger: 'ses_y' }]))
  assert.equal(result.ok, false)
  assert.equal(result.error, 'CompanionWouldRecurse')
  assert.match(result.message, /COMPANION-002/)
})

// SESSION-ONTOLOGY-009: role is not an input to association.
test('WHAT[SESSION-ONTOLOGY-009] COMPANION_001_every_work_session_may_have_a_companion', () => {
  const roles = ['orchestrator', 'manager', 'coder', 'inspector', 'browser', 'inquiry', 'reviewer', 'devops', 'distiller']
  const state = linked(roles.map((role) => ({ main: `ses_${role}`, blogger: `ses_${role}_y` })))
  for (const role of roles) {
    assert.equal(assoc.bloggerOf(`ses_${role}`, state), `ses_${role}_y`)
    assert.equal(assoc.isCompanion(`ses_${role}_y`, state), true)
  }
})

test('WHAT[SESSION-ONTOLOGY-008] COMPANION_003_relinking_same_pair_is_idempotent', () => {
  const once = linked([{ main: 'ses_x', blogger: 'ses_y' }])
  const twice = linked([{ main: 'ses_x', blogger: 'ses_y' }], once)
  assert.deepEqual(assoc.entry('ses_x', twice), assoc.entry('ses_x', once))
  assert.deepEqual(assoc.entry('ses_y', twice), assoc.entry('ses_y', once))
  assert.deepEqual(assoc.ids(twice), ['ses_x', 'ses_y'])
})

test('WHAT[SESSION-ONTOLOGY-008] COMPANION_002_second_companion_for_one_work_session_is_refused', () => {
  const result = assoc.link({ main: 'ses_x', blogger: 'ses_y2' }, linked([{ main: 'ses_x', blogger: 'ses_y1' }]))
  assert.equal(result.ok, false)
  assert.equal(result.error, 'AlreadyLinkedToOther')
  assert.match(result.message, /ses_y1/)
  assert.match(result.message, /ses_y2/)
})

test('WHAT[SESSION-ONTOLOGY-005] HOST_008_companion_cannot_serve_two_work_sessions', () => {
  const result = assoc.link({ main: 'ses_x2', blogger: 'ses_y' }, linked([{ main: 'ses_x1', blogger: 'ses_y' }]))
  assert.equal(result.ok, false)
  assert.equal(result.error, 'CompanionClaimedByOther')
  assert.match(result.message, /ses_x1/)
})

test('WHAT[SESSION-ONTOLOGY-005] HOST_008_session_cannot_be_its_own_companion', () => {
  const result = assoc.link({ main: 'ses_x', blogger: 'ses_x' }, assoc.empty)
  assert.equal(result.ok, false)
  assert.equal(result.error, 'SelfLink')
})

// SESSION-ONTOLOGY-006: logical parent is retained independently of physical placement.
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

test('WHAT[SESSION-ONTOLOGY-009] COMPANION_003_unlinking_frees_work_session_for_fresh_companion', () => {
  const state = linked([{ main: 'ses_x', blogger: 'ses_y1' }])
  const unlinked = assoc.unlink('ses_x', state)
  assert.equal(assoc.bloggerOf('ses_x', unlinked), null)
  assert.equal(assoc.entry('ses_x', unlinked).kind, 'WorkSession')
  assert.equal(assoc.entry('ses_y1', unlinked), null)
  assert.equal(assoc.isCompanion('ses_y1', unlinked), false)
  assert.deepEqual(assoc.ids(unlinked), ['ses_x'])
  assert.equal(assoc.bloggerOf('ses_x', linked([{ main: 'ses_x', blogger: 'ses_y2' }], unlinked)), 'ses_y2')
})

test('WHAT[SESSION-ONTOLOGY-009] COMPANION_003_unlinking_is_total_and_idempotent', () => {
  assert.deepEqual(assoc.ids(assoc.unlink('ses_never_seen', assoc.empty)), [])
  const once = assoc.unlink('ses_x', linked([{ main: 'ses_x', blogger: 'ses_y' }]))
  const twice = assoc.unlink('ses_x', once)
  assert.deepEqual(assoc.ids(twice), assoc.ids(once))
  assert.deepEqual(assoc.entry('ses_x', twice), assoc.entry('ses_x', once))
})

test('WHAT[SESSION-ONTOLOGY-008] COMPANION_002_unlinking_does_not_disturb_another_pair', () => {
  const state = linked([
    { main: 'ses_x1', blogger: 'ses_y1' },
    { main: 'ses_x2', blogger: 'ses_y2' },
  ])
  const unlinked = assoc.unlink('ses_x1', state)
  assert.equal(assoc.bloggerOf('ses_x2', unlinked), 'ses_y2')
  assert.equal(assoc.isCompanion('ses_y2', unlinked), true)
  assert.deepEqual(assoc.ids(unlinked), ['ses_x1', 'ses_x2', 'ses_y2'])
})
