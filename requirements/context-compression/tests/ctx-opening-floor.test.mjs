// CTX-016 / TODO-001 — the Opening floor is a structural cursor derived from
// LifeOpened + XTrace, never from WorkActivated / planning-stage business.
//
// This file pins the context-compression side: Blogger/Y effective start is
// `max(RecordCoverage, WorkRecordStart)`. T1 never expands that compression
// floor beyond the true Opening, and todowrite rounds survive X→Y replacement
// as raw X messages.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import * as compression from '../../../dist/Context/Companion/CompressionSurface.js'
import * as prefix from '../../../dist/Context/Prefix/Surface.js'

const floor = ({ hasOpenLife = true, planCommitted = false, xTraceHeadSequence = 0, legacyProtectedPrefixEnd } = {}) =>
  compression.openingFloor({
    hasOpenLife,
    planCommitted,
    openingSequence: 1,
    xTraceHeadSequence,
    legacyProtectedPrefixEnd,
    parts: [],
  })

test('WHAT[CONTEXT-COMPRESSION-017] CTX_016_pre_t1_floor_stops_after_true_opening', () => {
  assert.equal(
    Number(floor({ planCommitted: false, xTraceHeadSequence: 17 })),
    2,
    'Pre-T1 planning material after the opening is ordinary compressible history',
  )
})

test('WHAT[CONTEXT-COMPRESSION-017] CTX_016_t1_does_not_change_the_compression_floor', () => {
  assert.equal(Number(floor({ planCommitted: false, xTraceHeadSequence: 17 })), 2)
  assert.equal(Number(floor({ planCommitted: true, xTraceHeadSequence: 17 })), 2)
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

test('WHAT[CONTEXT-COMPRESSION-020] todowrite call and matching result are retained across a Y cutoff', () => {
  assert.deepEqual(
    prefix.retainTodoWriteRounds([
      { containsTodoWrite: false, callIds: [] },
      { containsTodoWrite: true, callIds: ['todo-call-1'] },
      { containsTodoWrite: false, callIds: ['todo-call-1'] },
      { containsTodoWrite: false, callIds: ['other-call'] },
    ]),
    [false, true, true, false],
    'only the todowrite round punches through an otherwise replaceable prefix',
  )
})

test('WHAT[CONTEXT-COMPRESSION-017] same_session_Y_prefix_never_repackages_or_deletes_the_raw_Opening', () => {
  const wire = readFileSync(new URL('../../../src/Wanxiangshu/Context/Prefix/Wire.fs', import.meta.url), 'utf8')

  const frozen = wire.slice(
    wire.indexOf('let private materializeFrozenRecordPrefix'),
    wire.indexOf('let private candidate'),
  )
  assert.match(frozen, /LifecycleWorkRecord\.render\s+false/)
  assert.doesNotMatch(frozen, /LifecycleWorkRecord\.render\s+true/)

  const replacement = wire.slice(
    wire.indexOf('let private requireStableReplacement'),
    wire.indexOf('let private commitPromotablePrefixRebase'),
  )
  assert.match(replacement, /XTraceProjection\.tryOpeningHostMessageId/)
  assert.match(replacement, /List\.filter/)
  assert.match(replacement, /applyRenderedPrefixByHostIds/)
})
