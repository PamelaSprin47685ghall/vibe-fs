// HOST-013: durable guideline pair ordinal succession.

import assert from 'node:assert/strict'
import test from 'node:test'

import { resultOf, caseOf, toList, listItems } from '../support/domain.mjs'

const GuidelineProjection = await import('../../../dist/Journal/GuidelineProjection.js')
const { ToolCallIdModule_create: toolCallId } = await import('../../../dist/Kernel/Identity.js')

const empty = GuidelineProjection.GuidelineProjection_empty
const nextOrdinal = GuidelineProjection.GuidelineProjection_nextOrdinal
const apply = GuidelineProjection.GuidelineProjection_apply
const pairs = GuidelineProjection.GuidelineProjection_pairs

test('HOST_013_nextOrdinal_starts_at_1_on_empty', () => {
  assert.equal(nextOrdinal(empty), 1n)
})

test('HOST_013_nextOrdinal_uses_last_pair_not_head', () => {
  let state = empty
  for (const n of [1n, 2n, 3n]) {
    const result = resultOf(apply(n, toolCallId(`call-${n}`), `text-${n}`, state))
    assert.equal(result.ok, true, `apply(${n}) must succeed: ${JSON.stringify(result)}`)
    state = result.value
  }
  assert.equal(listItems(pairs(state)).length, 3)
  assert.equal(nextOrdinal(state), 4n, 'successor after 1,2,3 must be 4, not head+1=2')
})

test('HOST_013_apply_rejects_non_successor_ordinal', () => {
  const first = resultOf(apply(1n, toolCallId('c1'), 't1', empty))
  assert.equal(first.ok, true)
  const second = resultOf(apply(2n, toolCallId('c2'), 't2', first.value))
  assert.equal(second.ok, true)
  const bad = resultOf(apply(2n, toolCallId('c3'), 't3', second.value))
  assert.equal(bad.ok, false)
  assert.equal(caseOf(bad.error), 'NonSequentialOrdinal')
})
