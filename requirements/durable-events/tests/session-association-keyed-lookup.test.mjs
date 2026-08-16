// Split from tests/unit/context/session-association.test.mjs (cutover Wave 2a); owner: durable-events.
//
// PERSIST-008: the SessionAssociationProjection answers both directions
// (`isCompanion` and `bloggerOf`) from one map via keyed lookups, without a scan
// — the two questions the transform boundary asks on every request cannot be
// answered from a reverse index that could disagree with the forward one.

import assert from 'node:assert/strict'
import test from 'node:test'
import { sessionAssociation as assoc } from '../../verification-system/tests/support/domain.mjs'

const linked = (pairs, start = assoc.empty) =>
  pairs.reduce((current, pair) => {
    const result = assoc.link(pair, current)
    assert.equal(result.ok, true, result.ok ? '' : result.message)
    return result.value
  }, start)

test('WHAT[DURABLE-EVENTS-013] PERSIST_008_both_directions_answer_from_one_map_without_a_scan', () => {
  // The reason both entries live in one map: `isCompanion` and `bloggerOf` are the
  // two questions the transform boundary asks on every request, and a reverse index
  // held separately could disagree with the forward one.
  const state = linked(
    Array.from({ length: 50 }, (_, n) => ({ main: `ses_x${n}`, blogger: `ses_y${n}` })),
  )

  assert.equal(assoc.ids(state).length, 100)
  assert.equal(assoc.bloggerOf('ses_x37', state), 'ses_y37')
  assert.equal(assoc.mainSessionOf('ses_y37', state), 'ses_x37')
  assert.equal(assoc.isCompanion('ses_y37', state), true)
  assert.equal(assoc.isCompanion('ses_x37', state), false)
})
