// HOST-013: durable auto-injected pair ordinal succession + anchored placement.

import assert from 'node:assert/strict'
import test from 'node:test'

import { resultOf, caseOf, toList, listItems, transcriptGap, transcriptAddress } from '../support/domain.mjs'

const GuidelineProjection = await import('../../../dist/Journal/GuidelineProjection.js')
const { ToolCallIdModule_create: toolCallId } = await import('../../../dist/Kernel/Identity.js')

const empty = GuidelineProjection.GuidelineProjection_empty
const nextOrdinal = GuidelineProjection.GuidelineProjection_nextOrdinal
const apply = GuidelineProjection.GuidelineProjection_apply
const pairs = GuidelineProjection.GuidelineProjection_pairs

const after = (id) => transcriptGap.after(transcriptAddress.create(id))
const before = (id) => transcriptGap.before(transcriptAddress.create(id))

test('HOST_013_nextOrdinal_starts_at_1_on_empty', () => {
  assert.equal(nextOrdinal(empty), 1n)
})

test('HOST_013_nextOrdinal_uses_last_pair_not_head', () => {
  let state = empty
  for (const n of [1n, 2n, 3n]) {
    const result = resultOf(apply(n, toolCallId(`call-${n}`), `text-${n}`, after(`m${n}`), after(`m${n}`), state))
    assert.equal(result.ok, true, `apply(${n}) must succeed: ${JSON.stringify(result)}`)
    state = result.value
  }
  assert.equal(listItems(pairs(state)).length, 3)
  assert.equal(nextOrdinal(state), 4n, 'successor after 1,2,3 must be 4, not head+1=2')
})

test('HOST_013_apply_rejects_non_successor_ordinal', () => {
  const first = resultOf(apply(1n, toolCallId('c1'), 't1', after('m1'), after('m1'), empty))
  assert.equal(first.ok, true)
  const second = resultOf(apply(2n, toolCallId('c2'), 't2', after('m2'), after('m2'), first.value))
  assert.equal(second.ok, true)
  const bad = resultOf(apply(2n, toolCallId('c3'), 't3', after('m3'), after('m3'), second.value))
  assert.equal(bad.ok, false)
  assert.equal(caseOf(bad.error), 'NonSequentialOrdinal')
})

test('HOST_013_apply_rejects_duplicate_call_id', () => {
  const first = resultOf(apply(1n, toolCallId('c1'), 't1', after('m1'), after('m1'), empty))
  assert.equal(first.ok, true)
  const bad = resultOf(apply(2n, toolCallId('c1'), 't2', after('m2'), after('m2'), first.value))
  assert.equal(bad.ok, false)
  assert.equal(caseOf(bad.error), 'DuplicateCallId')
})

test('HOST_013_apply_rejects_duplicate_placement', () => {
  // HOST-013 §8: one placement identity (SessionId + CallGap + ResultGap)
  // admits at most one pair. The projection is per-session, so the session
  // part of the identity is implicit.
  const first = resultOf(apply(1n, toolCallId('c1'), 't1', before('u1'), before('u1'), empty))
  assert.equal(first.ok, true)
  const bad = resultOf(apply(2n, toolCallId('c2'), 't2', before('u1'), before('u1'), first.value))
  assert.equal(bad.ok, false)
  assert.equal(caseOf(bad.error), 'DuplicatePlacement')

  // A different gap pair is a different placement.
  const ok = resultOf(apply(2n, toolCallId('c2'), 't2', after('m1'), after('m1'), first.value))
  assert.equal(ok.ok, true)
})
