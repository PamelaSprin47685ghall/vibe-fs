import assert from 'node:assert/strict'
import test from 'node:test'
import * as gp from '../../../dist/Enforcer/GuidelineProjectionSurface.js'
import * as pgc from '../../../dist/Enforcer/PairGapConstructorSurface.js'
import * as guidelineSurface from '../../../dist/OpenCode/Host/PairProgramming/GuidelineSurface.js'

test('WHAT[GD-011] GP_001_empty_state_starts_ordinal_at_one', () => {
  const state = gp.createState()
  assert.equal(gp.nextOrdinal(state), 1)
})

test('WHAT[GD-011] GP_002_apply_records_pair_and_restores_marker_bytes', () => {
  const state = gp.createState()
  gp.applyPair(state, { ordinal: 1, callId: 'c1', markerText: 'exact-marker-bytes' })
  assert.equal(gp.markerBytes(state, 1), 'exact-marker-bytes')
})

test('WHAT[GD-011] GP_003_non_sequential_ordinal_is_rejected', () => {
  const state = gp.createState()
  assert.throws(() => gp.applyPair(state, { ordinal: 2, callId: 'c1', markerText: 'bytes' }), /ordinal mismatch/)
})

test('WHAT[GD-011] GP_004_duplicate_call_id_is_rejected', () => {
  const state = gp.createState()
  gp.applyPair(state, { ordinal: 1, callId: 'c1', markerText: 'bytes1' })
  assert.throws(() => gp.applyPair(state, { ordinal: 2, callId: 'c1', markerText: 'bytes2' }), /duplicate call id/)
})

test('WHAT[GD-011] GP_005_duplicate_placement_is_rejected', () => {
  const state = gp.createState()
  gp.applyPair(state, { ordinal: 1, callId: 'c1', placement: 'p1', markerText: 'bytes1' })
  assert.throws(() => gp.applyPair(state, { ordinal: 2, callId: 'c2', placement: 'p1', markerText: 'bytes2' }), /duplicate placement/)
})

test('WHAT[GD-011] GP_006_replay_restores_pairs_oldest_first', () => {
  const state = gp.createState()
  gp.applyPair(state, { ordinal: 1, callId: 'c1', markerText: 'b1' })
  gp.applyPair(state, { ordinal: 2, callId: 'c2', markerText: 'b2' })
  const pairs = gp.allPairs(state)
  assert.deepEqual(pairs.map((p) => p.ordinal), [1, 2])
})

test('WHAT[GD-011] GP_007_reanchor_retires_visible_pairs_without_deleting_durable_history', () => {
  const state = gp.createState()
  gp.applyPair(state, { ordinal: 1, callId: 'c1', markerText: 'b1' })
  gp.reanchor(state)
  assert.equal(gp.visiblePairs(state).length, 0)
  assert.equal(gp.allPairs(state).length, 1)
})

test('WHAT[GD-011] PPT_gap_constructor_receives_the_same_address_exactly_twice_in_pair_order', () => {
  const addresses = pgc.constructGapPair('call-1', 'result-1')
  assert.equal(addresses.length, 2)
  assert.equal(addresses[0].target, 'call-1')
  assert.equal(addresses[1].target, 'result-1')
})

test('WHAT[GD-011] PPT_gap_constructor_failure_propagates_from_the_first_call_and_stops_the_pair', () => {
  assert.throws(() => pgc.constructGapPair(null, 'result-1'), /invalid call gap address/)
})
