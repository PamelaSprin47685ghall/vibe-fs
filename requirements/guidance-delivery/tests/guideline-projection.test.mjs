// GuidelineProjection — durable auto-injected pair fold (HOST-013).
//
// The Main guidance marker is anchored to two transcript gaps (call + result)
// and stored append-only per session, so a replay restores every historical
// half at its original position with the exact MarkerText bytes that were
// actually sent. The fold rejects non-sequential ordinals, duplicate call ids
// and duplicate placements (one placement identity → at most one pair).
import assert from 'node:assert/strict'
import test from 'node:test'
import {
  listItems,
  toolCallId,
  transcriptAddress,
  transcriptGap,
} from '../../verification-system/tests/support/domain.mjs'
import {
  GuidelineProjection_empty as empty,
  GuidelineProjection_apply as apply,
  GuidelineProjection_pairs as pairs,
  GuidelineProjection_nextOrdinal as nextOrdinal,
} from '../../../dist/OpenCode/Host/PairProgramming/GuidelineProjection.js'

const addr = (value) => transcriptAddress.create(value)
const gapBefore = (value) => transcriptGap.before(addr(value))
const gapAfter = (value) => transcriptGap.after(addr(value))
const marker = 'tip: primitive-obsession'

// Fable DU case name (Ok | Error) — never the positional tag ordinal.
const resultTag = (r) => r.name

test('WHAT[GD-011] GP_001_empty_state_starts_ordinal_at_one', () => {
  assert.equal(nextOrdinal(empty), 1n)
  assert.deepEqual(listItems(pairs(empty)), [])
})

test('WHAT[GD-011] GP_002_apply_records_pair_and_restores_marker_bytes', () => {
  const callGap = gapBefore('msg-3')
  const resultGap = gapAfter('msg-3')
  const result = apply(1n, toolCallId('call-1'), marker, callGap, resultGap, empty)
  assert.equal(resultTag(result), 'Ok', 'first apply must fold Ok')

  const state = result.toJSON()[1]
  assert.equal(nextOrdinal(state), 2n)

  const restored = listItems(pairs(state))
  assert.equal(restored.length, 1)
  // Byte-identical replay of what was actually sent (HOST-013 MarkerText).
  assert.equal(restored[0].MarkerText, marker)
  assert.equal(restored[0].Ordinal, 1n)
  assert.equal(restored[0].CallGap.name, callGap.name)
  assert.equal(restored[0].ResultGap.name, resultGap.name)
})

test('WHAT[GD-011] GP_003_non_sequential_ordinal_is_rejected', () => {
  const result = apply(3n, toolCallId('call-1'), marker, gapBefore('m'), gapAfter('m'), empty)
  assert.equal(resultTag(result), 'Error')
  const rejection = result.toJSON()[1]
  assert.equal(rejection.name, 'NonSequentialOrdinal', 'GuidelineFoldRejection.NonSequentialOrdinal')
  assert.equal(rejection.toJSON()[1], 1n, 'expected ordinal')
  assert.equal(rejection.toJSON()[2], 3n, 'actual ordinal')
})

test('WHAT[GD-011] GP_004_duplicate_call_id_is_rejected', () => {
  const first = apply(1n, toolCallId('call-x'), marker, gapBefore('a'), gapAfter('a'), empty)
  assert.equal(resultTag(first), 'Ok')
  const second = apply(2n, toolCallId('call-x'), marker, gapBefore('b'), gapAfter('b'), first.toJSON()[1])
  assert.equal(resultTag(second), 'Error')
  assert.equal(second.toJSON()[1].name, 'DuplicateCallId', 'GuidelineFoldRejection.DuplicateCallId')
})

test('WHAT[GD-011] GP_005_duplicate_placement_is_rejected', () => {
  const first = apply(1n, toolCallId('call-1'), marker, gapBefore('p'), gapAfter('p'), empty)
  assert.equal(resultTag(first), 'Ok')
  const second = apply(2n, toolCallId('call-2'), marker, gapBefore('p'), gapAfter('p'), first.toJSON()[1])
  assert.equal(resultTag(second), 'Error')
  assert.equal(second.toJSON()[1].name, 'DuplicatePlacement', 'GuidelineFoldRejection.DuplicatePlacement')
})

test('WHAT[GD-011] GP_006_replay_restores_pairs_oldest_first', () => {
  let state = empty
  for (let n = 1n; n <= 3n; n++) {
    const result = apply(n, toolCallId(`call-${n}`), `${marker} ${n}`, gapBefore(`m${n}`), gapAfter(`m${n}`), state)
    assert.equal(resultTag(result), 'Ok')
    state = result.toJSON()[1]
  }
  const restored = listItems(pairs(state))
  assert.deepEqual(
    restored.map((p) => p.Ordinal),
    [1n, 2n, 3n],
    'pairs must restore oldest-first',
  )
  assert.deepEqual(
    restored.map((p) => p.MarkerText),
    [`${marker} 1`, `${marker} 2`, `${marker} 3`],
    'exact marker bytes must survive replay',
  )
})
