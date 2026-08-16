// CTX-016 / TODO-001 — the Opening floor is a structural cursor derived from
// LifeOpened + XTrace, never from WorkActivated / planning-stage business.
//
// This file pins the context-compression side: Blogger/Y effective start is
// `max(RecordCoverage, WorkRecordStart)`, the floor comes from the XTrace
// head while Opening is open (Pre-T1), and the legacy WorkActivated fact is
// inert — supplying it must not move the floor.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as compression from '../../../dist/Context/Companion/CompressionSurface.js'

const floor = ({ hasOpenLife = true, planCommitted = false, xTraceHeadSequence = 0, legacyProtectedPrefixEnd } = {}) =>
  compression.openingFloor({
    hasOpenLife,
    planCommitted,
    openingSequence: 1,
    xTraceHeadSequence,
    legacyProtectedPrefixEnd,
    parts: [],
  })

test('WHAT[CONTEXT-COMPRESSION-017] CTX_016_pre_t1_floor_is_the_xtrace_head_not_an_activation_cursor', () => {
  // LifeOpened + two XTrace parts; no todowrite accepted yet → Opening still
  // open, Blogger must not start before the XTrace head (structural floor).
  assert.equal(Number(floor({ xTraceHeadSequence: 2 })), 2, 'Pre-T1 floor = XTrace head (exclusive)')
})

test('WHAT[CONTEXT-COMPRESSION-017] CTX_016_work_activated_is_inert_and_does_not_move_the_floor', () => {
  const without = Number(floor({ xTraceHeadSequence: 2 }))
  const withLegacy = Number(floor({ xTraceHeadSequence: 2, legacyProtectedPrefixEnd: 42 }))

  assert.equal(withLegacy, without, 'WorkActivated (inert legacy) must not change the structural floor')
  assert.notEqual(withLegacy, 42, 'the legacy ProtectedPrefixEndSequence (42) must never be read')
})

test('WHAT[CONTEXT-COMPRESSION-017] CTX_016_blogger_effective_start_is_max_of_record_coverage_and_floor', () => {
  assert.equal(
    Number(compression.bloggerEffectiveStart(1, 3)),
    3,
    'coverage behind floor → effective start = floor',
  )

  assert.equal(
    Number(compression.bloggerEffectiveStart(5, 3)),
    5,
    'coverage ahead of floor → effective start = record coverage',
  )
})
