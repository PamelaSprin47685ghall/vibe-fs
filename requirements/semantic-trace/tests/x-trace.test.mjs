// COMPANION-003 / HOST-005 — XTrace: X 的唯一原始语义轨迹。
//
// 覆盖：cursor 单调、slice 边界、head、flatten 单一 source、canonical render。
// 命名直接引用条款（VERIFY 命名原则）。

import { test } from 'node:test'
import assert from 'node:assert/strict'
import { xTrace } from '../../verification-system/tests/support/domain.mjs'

test('XTRACE_cursor_is_strictly_monotonic', () => {
  const origin = xTrace.originCursor
  const second = xTrace.next(origin)
  const third = xTrace.next(second)

  assert.equal(origin.Sequence, 0)
  assert.equal(second.Sequence, 1)
  assert.equal(third.Sequence, 2)
  assert.equal(xTrace.isAfter(second, origin), true)
  assert.equal(xTrace.isAfter(origin, origin), false)
  // 同 cursor 重复 append 是 PERSIST-010 拒绝条件：不是 after。
  assert.equal(xTrace.isAfter(second, second), false)
})

test('XTRACE_slice_between_is_half_open_and_order_preserving', () => {
  const items = [
    xTrace.item({ sequence: 0, part: xTrace.text('a') }),
    xTrace.item({ sequence: 1, part: xTrace.text('b') }),
    xTrace.item({ sequence: 2, part: xTrace.text('c') }),
    xTrace.item({ sequence: 3, part: xTrace.text('d') }),
  ]

  const middle = xTrace.sliceBetween({ Sequence: 1 }, { Sequence: 3 }, items)
  assert.deepEqual(middle.map((item) => item.Cursor.Sequence), [1, 2])
})

test('XTRACE_slice_from_takes_suffix_to_head', () => {
  const items = [
    xTrace.item({ sequence: 0, part: xTrace.text('a') }),
    xTrace.item({ sequence: 1, part: xTrace.text('b') }),
    xTrace.item({ sequence: 2, part: xTrace.text('c') }),
  ]

  const suffix = xTrace.sliceFrom({ Sequence: 1 }, items)
  assert.deepEqual(suffix.map((item) => item.Cursor.Sequence), [1, 2])
})

test('XTRACE_head_is_after_last_item_and_origin_for_empty', () => {
  assert.equal(xTrace.head([]).Sequence, 0)

  const items = [xTrace.item({ sequence: 4, part: xTrace.text('x') })]
  assert.equal(xTrace.head(items).Sequence, 5)
})

test('XTRACE_flatten_is_the_single_semantic_source', () => {
  const turns = [
    { role: 'user', parts: [xTrace.text('Fix the race.'), xTrace.toolCall('read', '{"path":"a"}')] },
    { role: 'assistant', parts: [xTrace.reasoning('considered'), xTrace.text('done')] },
  ]

  const flat = xTrace.flatten(turns)
  assert.equal(flat.length, 4)
  assert.deepEqual(
    flat.map((entry) => entry.role),
    ['user', 'user', 'assistant', 'assistant'],
  )
  assert.equal(flat[2].part.tag, xTrace.reasoning('x').tag)
})

test('XTRACE_render_is_deterministic_and_never_emits_provenance', () => {
  const items = [
    xTrace.item({ sequence: 0, role: 'user', provenance: 'run-1/msg-1', part: xTrace.text('Fix the race.') }),
    xTrace.item({ sequence: 1, role: 'assistant', provenance: 'run-1/msg-1', part: xTrace.reasoning('hidden consideration') }),
    xTrace.item({ sequence: 2, role: 'assistant', provenance: 'run-1/msg-1', part: xTrace.toolCall('read', '{"path":"a"}') }),
    xTrace.item({ sequence: 3, role: 'assistant', provenance: 'run-1/msg-1', part: xTrace.media('image/png', 'digest-1') }),
  ]

  const first = xTrace.render(xTrace.toItems(items))
  const second = xTrace.render(xTrace.toItems(items))

  assert.equal(first, second)
  assert.match(first, /user: Fix the race\./)
  assert.match(first, /hidden consideration/)
  assert.match(first, /\[tool call\] read \{"path":"a"\}/)
  assert.match(first, /\[media omitted: image\/png\]/)
  // provenance 永不输出（HOST-005）
  assert.equal(first.includes('run-1'), false)
  assert.equal(first.includes('msg-1'), false)
})

test('XTRACE_empty_render_is_empty_string', () => {
  assert.equal(xTrace.render(xTrace.toItems([])), '')
})

test('XTRACE_forWorkRecord_drops_raw_tools_keeps_text_reasoning_media', () => {
  // COMPANION-003: XTrace 全量可含 tool；LWR 投影剔除 raw tool。
  const items = [
    xTrace.item({ sequence: 0, role: 'user', part: xTrace.text('task') }),
    xTrace.item({ sequence: 1, role: 'assistant', part: xTrace.reasoning('why') }),
    xTrace.item({ sequence: 2, role: 'assistant', part: xTrace.toolCall('read', '{}') }),
    xTrace.item({ sequence: 3, role: 'assistant', part: xTrace.toolResult('payload') }),
    xTrace.item({ sequence: 4, role: 'assistant', part: xTrace.media('image/png', 'd') }),
  ]

  const filtered = xTrace.forWorkRecord(items)
  assert.deepEqual(
    filtered.map((item) => item.Cursor.Sequence),
    [0, 1, 4],
  )
  assert.equal(xTrace.isWorkRecordPart(xTrace.toolCall('read', '{}')), false)
  assert.equal(xTrace.isWorkRecordPart(xTrace.toolResult('x')), false)
  assert.equal(xTrace.isWorkRecordPart(xTrace.text('x')), true)
})
