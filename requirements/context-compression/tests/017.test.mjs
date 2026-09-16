import assert from 'node:assert/strict'
import test from 'node:test'
import * as floor from '../../../dist/Context/Companion/OpeningFloorSurface.js'
import * as companion from '../../../dist/Context/Companion/CompanionSurface.js'

test('WHAT[CONTEXT-COMPRESSION-017] COMPANION_010_same_session_lwr_returns_responsibility_without_delegation_fields', () => {
  assert.ok(companion.lwrWithoutDelegationFields)
})

test('WHAT[CONTEXT-COMPRESSION-017] CTX_016_pre_t1_floor_stops_after_true_opening', () => {
  assert.equal(floor.floorStopsAfterTrueOpening, true)
})

test('WHAT[CONTEXT-COMPRESSION-017] CTX_016_t1_does_not_change_the_compression_floor', () => {
  assert.equal(floor.t1DoesNotChangeFloor, true)
})

test('WHAT[CONTEXT-COMPRESSION-017] CTX_016_work_activated_is_inert_and_does_not_move_the_floor', () => {
  assert.equal(floor.workActivatedIsInert, true)
})

test('WHAT[CONTEXT-COMPRESSION-017] CTX_016_blogger_effective_start_is_max_of_record_coverage_and_floor', () => {
  assert.equal(floor.effectiveStart(5, 10), 10)
  assert.equal(floor.effectiveStart(15, 10), 15)
})
