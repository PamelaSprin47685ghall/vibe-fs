import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const guideline = await import("../../../dist/OpenCode/Host/PairProgramming/GuidelineSurface.js");

const empty = guideline.empty
const apply = (ordinal, callId, markerText, callGap, resultGap, state) =>
  guideline.apply({ ordinal, callId, markerText, callGap, resultGap }, state)
const pairs = guideline.pairs
const nextOrdinal = guideline.nextOrdinal
const gapBefore = (value) => `before:${value}`
const gapAfter = (value) => `after:${value}`
const marker = 'tip: primitive-obsession'
const resultTag = (result) => (result.ok ? 'Ok' : 'Error')

test('WHAT[GD-011] GP_001_empty_state_starts_ordinal_at_one', () => {
  assert.equal(nextOrdinal(empty), 1n)
  assert.deepEqual(pairs(empty), [])
})
test('WHAT[GD-011] GP_002_apply_records_pair_and_restores_marker_bytes', () => {
  const callGap = gapBefore('msg-3')
  const resultGap = gapAfter('msg-3')
  const result = apply(1n, 'call-1', marker, callGap, resultGap, empty)
  assert.equal(resultTag(result), 'Ok', 'first apply must fold Ok')

  const state = result.value
  assert.equal(nextOrdinal(state), 2n)

  const restored = pairs(state)
  assert.equal(restored.length, 1)
  // Byte-identical replay of what was actually sent (HOST-013 MarkerText).
  assert.equal(restored[0].markerText, marker)
  assert.equal(restored[0].ordinal, 1n)
  assert.equal(restored[0].callGap, callGap)
  assert.equal(restored[0].resultGap, resultGap)
})
test('WHAT[GD-011] GP_003_non_sequential_ordinal_is_rejected', () => {
  const result = apply(3n, 'call-1', marker, gapBefore('m'), gapAfter('m'), empty)
  assert.equal(resultTag(result), 'Error')
  const rejection = result.error
  assert.equal(rejection.name, 'NonSequentialOrdinal', 'GuidelineFoldRejection.NonSequentialOrdinal')
  assert.equal(rejection.expected, 1n, 'expected ordinal')
  assert.equal(rejection.actual, 3n, 'actual ordinal')
})
test('WHAT[GD-011] GP_004_duplicate_call_id_is_rejected', () => {
  const first = apply(1n, 'call-x', marker, gapBefore('a'), gapAfter('a'), empty)
  assert.equal(resultTag(first), 'Ok')
  const second = apply(2n, 'call-x', marker, gapBefore('b'), gapAfter('b'), first.value)
  assert.equal(resultTag(second), 'Error')
  assert.equal(second.error.name, 'DuplicateCallId', 'GuidelineFoldRejection.DuplicateCallId')
})
test('WHAT[GD-011] GP_005_duplicate_placement_is_rejected', () => {
  const first = apply(1n, 'call-1', marker, gapBefore('p'), gapAfter('p'), empty)
  assert.equal(resultTag(first), 'Ok')
  const second = apply(2n, 'call-2', marker, gapBefore('p'), gapAfter('p'), first.value)
  assert.equal(resultTag(second), 'Error')
  assert.equal(second.error.name, 'DuplicatePlacement', 'GuidelineFoldRejection.DuplicatePlacement')
})
test('WHAT[GD-011] GP_006_replay_restores_pairs_oldest_first', () => {
  let state = empty
  for (let n = 1n; n <= 3n; n++) {
    const result = apply(n, `call-${n}`, `${marker} ${n}`, gapBefore(`m${n}`), gapAfter(`m${n}`), state)
    assert.equal(resultTag(result), 'Ok')
    state = result.value
  }
  const restored = pairs(state)
  assert.deepEqual(
    restored.map((p) => p.ordinal),
    [1n, 2n, 3n],
    'pairs must restore oldest-first',
  )
  assert.deepEqual(
    restored.map((p) => p.markerText),
    [`${marker} 1`, `${marker} 2`, `${marker} 3`],
    'exact marker bytes must survive replay',
  )
})
test('WHAT[GD-011] GP_007_reanchor_retires_visible_pairs_without_deleting_durable_history', () => {
  const first = apply(1n, 'call-1', marker, gapBefore('m1'), gapAfter('m1'), empty)
  assert.equal(resultTag(first), 'Ok')
  assert.equal(guideline.visiblePairs(first.value).length, 1)

  const reanchored = guideline.applyReanchor(first.value)
  assert.deepEqual(guideline.visiblePairs(reanchored), [])
  assert.equal(pairs(reanchored).length, 1, 'durable history remains auditable')
  assert.equal(nextOrdinal(reanchored), 2n, 'ordinal frontier never rewinds')

  const second = apply(2n, 'call-2', marker, gapBefore('m1'), gapAfter('m1'), reanchored)
  assert.equal(resultTag(second), 'Ok', 'same gap may be reused only because it is a new horizon')
  assert.deepEqual(guideline.visiblePairs(second.value).map((pair) => pair.callId), ['call-2'])
  assert.deepEqual(pairs(second.value).map((pair) => pair.callId), ['call-1', 'call-2'])
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const pair = await import("../../../dist/OpenCode/Host/PairProgrammingThoughtSurface.js");


test('WHAT[GD-011] PPT_gap_constructor_receives_the_same_address_exactly_twice_in_pair_order', () => {
  const trace = pair.gapConstructorTrace('message-address-17')

  assert.equal(trace.ok, true)
  assert.deepEqual(trace.inputs, ['message-address-17', 'message-address-17'])
  assert.equal(trace.left, 'after:message-address-17')
  assert.equal(trace.right, 'after:message-address-17')
})
test('WHAT[GD-011] PPT_gap_constructor_failure_propagates_from_the_first_call_and_stops_the_pair', () => {
  const trace = pair.gapConstructorFailureTrace('message-address-failure')

  assert.equal(trace.calls, 1)
  assert.equal(trace.error, 'gap-constructor-failed')
})
}
