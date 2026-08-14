// Split from tests/unit/context/session-association.test.mjs (cutover Wave 2a); owner: session-ontology.
// (PERSIST_008_both_directions_answer_from_one_map_without_a_scan went to durable-events.)
//
// HOST-008 / COMPANION-001/002/003.
//
// Companion is a Session-kind fact, not a role entitlement. This file exists to pin
// that replacement: the question is "is this session itself a Companion", never "does
// this role deserve one".
//
// The deleted alternative was `hasCompanion(role)` — a whitelist over ten roles that
// silently excluded Inspector, Browser and Executor. It could not be fixed by editing
// the list, because a whitelist is the wrong shape: COMPANION-001 gives every managed
// work session a Y regardless of role, and the only thing that must NOT have one is a
// Y itself.
//
// Both directions of the link are written by one fact. A projection holding `X → Y`
// without `Y → CompanionSession X` would answer `isCompanion(Y)` with false, and the
// next transform on Y would give it a Y of its own.

import assert from 'node:assert/strict'
import test from 'node:test'
import { sessionAssociation as assoc } from '../../verification-system/tests/support/domain.mjs'

const linked = (pairs, start = assoc.empty) =>
  pairs.reduce((current, pair) => {
    const result = assoc.link(pair, current)
    assert.equal(result.ok, true, result.ok ? '' : result.message)
    return result.value
  }, start)

// ── the empty state ────────────────────────────────────────────────────────

test('COMPANION_001_an_unknown_session_is_not_a_Companion', () => {
  // "No record yet" means a work session whose Y has not been lazily created — the
  // state the next transform resolves. It never means an unheard-of Companion: a Y's
  // association is written before its first prompt.
  assert.equal(assoc.isCompanion('ses_unknown', assoc.empty), false)
  assert.equal(assoc.bloggerOf('ses_unknown', assoc.empty), undefined)
  assert.equal(assoc.entry('ses_unknown', assoc.empty), undefined)
})

// ── one fact writes both directions ────────────────────────────────────────

test('HOST_008_linking_records_both_directions_at_once', () => {
  const state = linked([{ main: 'ses_x', blogger: 'ses_y' }])

  assert.deepEqual(assoc.entry('ses_x', state), {
    kind: 'WorkSession',
    mainSessionId: undefined,
    satelliteKind: undefined,
    blogger: 'ses_y',
    parent: undefined,
  })

  assert.deepEqual(assoc.entry('ses_y', state), {
    kind: 'SatelliteSession',
    mainSessionId: 'ses_x',
    satelliteKind: 'Companion',
    blogger: undefined,
    parent: 'ses_x',
  })

  assert.deepEqual(assoc.ids(state), ['ses_x', 'ses_y'])
})

test('COMPANION_002_the_Companion_side_answers_isCompanion_immediately', () => {
  // The property that makes the recursion impossible. If this were false, the next
  // transform on ses_y would treat it as an ordinary work session.
  const state = linked([{ main: 'ses_x', blogger: 'ses_y' }])

  assert.equal(assoc.isCompanion('ses_y', state), true)
  assert.equal(assoc.isCompanion('ses_x', state), false)
  assert.equal(assoc.mainSessionOf('ses_y', state), 'ses_x')
  assert.equal(assoc.mainSessionOf('ses_x', state), undefined)
})

test('COMPANION_002_a_Companion_is_structurally_a_leaf', () => {
  // `BloggerSessionId = None` on the Companion side is not a value a caller supplies
  // and could get wrong — `link` writes it.
  const state = linked([{ main: 'ses_x', blogger: 'ses_y' }])

  assert.equal(assoc.bloggerOf('ses_y', state), undefined)
})

test('COMPANION_002_giving_a_Companion_its_own_Companion_is_refused', () => {
  const state = linked([{ main: 'ses_x', blogger: 'ses_y' }])

  const recursion = assoc.link({ main: 'ses_y', blogger: 'ses_z' }, state)

  assert.equal(recursion.ok, false)
  assert.equal(recursion.error, 'CompanionWouldRecurse')
  assert.match(recursion.message, /COMPANION-002/)
})

// ── COMPANION-001: role plays no part ──────────────────────────────────────

test('COMPANION_001_every_work_session_may_have_a_Y_regardless_of_role', () => {
  // Ten role-shaped session ids, including the three the deleted whitelist excluded.
  // The association API takes no role at all, which is the structural statement: a
  // role cannot influence a decision it is not an input to.
  const roles = [
    'orchestrator',
    'manager',
    'coder',
    'inspector',
    'browser',
    'inquiry',
    'reviewer',
    'devops',
    'distiller',
  ]

  const state = linked(roles.map((role) => ({ main: `ses_${role}`, blogger: `ses_${role}_y` })))

  for (const role of roles) {
    assert.equal(assoc.bloggerOf(`ses_${role}`, state), `ses_${role}_y`, `${role} must have a Companion`)
    assert.equal(assoc.isCompanion(`ses_${role}_y`, state), true)
  }

  // Inspector, Browser and Distiller in particular: the whitelist marked these three
  // as having no Companion, so three of the ten silently never got one.
  for (const role of ['inspector', 'browser', 'distiller']) {
    assert.equal(assoc.bloggerOf(`ses_${role}`, state), `ses_${role}_y`)
  }
})

// ── COMPANION-003: exactly one Y, reused across restarts ───────────────────

test('COMPANION_003_relinking_the_same_pair_is_idempotent', () => {
  // Restart recovery re-attempts the link. Refusing it would turn recovery into a
  // startup failure.
  const once = linked([{ main: 'ses_x', blogger: 'ses_y' }])
  const twice = linked([{ main: 'ses_x', blogger: 'ses_y' }], once)

  assert.deepEqual(assoc.entry('ses_x', twice), assoc.entry('ses_x', once))
  assert.deepEqual(assoc.entry('ses_y', twice), assoc.entry('ses_y', once))
  assert.deepEqual(assoc.ids(twice), ['ses_x', 'ses_y'])
})

test('COMPANION_002_a_second_different_Y_for_one_work_session_is_refused', () => {
  // The lazy-creation rule exists to prevent exactly this. Silently repointing would
  // orphan the first Y with its whole frame history still in the journal.
  const state = linked([{ main: 'ses_x', blogger: 'ses_y1' }])

  const second = assoc.link({ main: 'ses_x', blogger: 'ses_y2' }, state)

  assert.equal(second.ok, false)
  assert.equal(second.error, 'AlreadyLinkedToOther')
  assert.match(second.message, /ses_y1/)
  assert.match(second.message, /ses_y2/)
})

test('HOST_008_one_Companion_cannot_serve_two_work_sessions', () => {
  const state = linked([{ main: 'ses_x1', blogger: 'ses_y' }])

  const stolen = assoc.link({ main: 'ses_x2', blogger: 'ses_y' }, state)

  assert.equal(stolen.ok, false)
  assert.equal(stolen.error, 'CompanionClaimedByOther')
  assert.match(stolen.message, /ses_x1/)
})

test('HOST_008_a_session_cannot_be_its_own_Companion', () => {
  const self = assoc.link({ main: 'ses_x', blogger: 'ses_x' }, assoc.empty)

  assert.equal(self.ok, false)
  assert.equal(self.error, 'SelfLink')
})

// ── parent lineage ─────────────────────────────────────────────────────────

test('HOST_008_the_work_session_parent_is_recorded_when_supplied', () => {
  const state = linked([{ main: 'ses_child', blogger: 'ses_child_y', parent: 'ses_parent' }])

  assert.equal(assoc.entry('ses_child', state).parent, 'ses_parent')

  // The Companion's parent is its own X, not X's parent: the Companion belongs to the
  // work session, and reporting the grandparent would make the relation ambiguous.
  assert.equal(assoc.entry('ses_child_y', state).parent, 'ses_child')
})

test('HOST_008_relinking_without_a_parent_does_not_erase_a_known_one', () => {
  // Restart recovery may know the pair without knowing the lineage. Overwriting with
  // `None` would lose a fact the journal already established.
  const withParent = linked([{ main: 'ses_child', blogger: 'ses_y', parent: 'ses_parent' }])
  const relinked = linked([{ main: 'ses_child', blogger: 'ses_y' }], withParent)

  assert.equal(assoc.entry('ses_child', relinked).parent, 'ses_parent')
})

// ── unlink ─────────────────────────────────────────────────────────────────

test('COMPANION_003_unlinking_frees_the_work_session_to_get_a_fresh_Y', () => {
  const state = linked([{ main: 'ses_x', blogger: 'ses_y1' }])
  const unlinked = assoc.unlink('ses_x', state)

  assert.equal(assoc.bloggerOf('ses_x', unlinked), undefined)
  assert.equal(assoc.entry('ses_x', unlinked).kind, 'WorkSession', 'the work session keeps its record')

  // The aborted Companion's entry is REMOVED, not kept as a tombstone. Unlike a
  // handle (EXEC-009), a Companion id is never re-presented by the model and never
  // joined, so there is no request a stale entry would have to refuse — and leaving
  // one would make isCompanion true for a session that no longer exists.
  assert.equal(assoc.entry('ses_y1', unlinked), undefined)
  assert.equal(assoc.isCompanion('ses_y1', unlinked), false)
  assert.deepEqual(assoc.ids(unlinked), ['ses_x'])

  const fresh = linked([{ main: 'ses_x', blogger: 'ses_y2' }], unlinked)
  assert.equal(assoc.bloggerOf('ses_x', fresh), 'ses_y2')
})

test('COMPANION_003_unlinking_is_total_and_idempotent', () => {
  // A replayed close, or a close for a session with no Y, is already in the state the
  // fact describes.
  assert.deepEqual(assoc.ids(assoc.unlink('ses_never_seen', assoc.empty)), [])

  const state = linked([{ main: 'ses_x', blogger: 'ses_y' }])
  const once = assoc.unlink('ses_x', state)
  const twice = assoc.unlink('ses_x', once)

  assert.deepEqual(assoc.ids(twice), assoc.ids(once))
  assert.deepEqual(assoc.entry('ses_x', twice), assoc.entry('ses_x', once))
})

test('COMPANION_002_unlinking_does_not_disturb_another_pair', () => {
  const state = linked([
    { main: 'ses_x1', blogger: 'ses_y1' },
    { main: 'ses_x2', blogger: 'ses_y2' },
  ])

  const unlinked = assoc.unlink('ses_x1', state)

  assert.equal(assoc.bloggerOf('ses_x2', unlinked), 'ses_y2')
  assert.equal(assoc.isCompanion('ses_y2', unlinked), true)
  assert.deepEqual(assoc.ids(unlinked), ['ses_x1', 'ses_x2', 'ses_y2'])
})
