// COMPANION-003 / EXEC-006 / EXEC-008 — LifecycleWorkRecord 物化。
//
// LWR = Opening + CompressedMiddleFromY + RawGapFromX + TerminalOutputRaw。
// 覆盖：byte-exact opening/terminal、gap 从 max(ingestedThrough, openingEnd) 起、
// 无 Y 时同一算法、空段省略、determinism、child opening 排除 parent envelope。

import { test } from 'node:test'
import assert from 'node:assert/strict'
import { xTrace, lifecycleWorkRecord } from '../support/domain.mjs'

const opening = (assignment, requirements = []) => lifecycleWorkRecord.opening({ assignment, requirements })

// 公共 fixture：trace 中 cursor 0 是 opening（Y 起点在 opening 之后，方案 4.1）
const OPENING_END = { Sequence: 1 }

test('LWR_opening_prompt_is_byte_exact_and_appears_exactly_once', () => {
  const assignment = 'Rewrite the fallback controller.\nKeep it typed.'
  const trace = [xTrace.item({ sequence: 0, role: 'user', part: xTrace.text(assignment) })]

  const rendered = lifecycleWorkRecord.materialize(opening(assignment), [], trace, { Sequence: 1 }, [], OPENING_END)

  assert.equal(rendered.includes('# Opening task'), true)
  // Opening 只出现在 # Opening task 段，不重复于 gap（openingEnd 之后的 gap 为空）
  assert.equal(rendered.split(assignment).length - 1, 1)
  // 首条 prompt 原文逐字，不 Trim、不重排
  assert.match(rendered, new RegExp(assignment.replace(/\n/g, '\\n')))
})

test('LWR_y_frames_cover_prefix_and_x_supplies_only_suffix', () => {
  const trace = [
    xTrace.item({ sequence: 0, role: 'user', part: xTrace.text('task') }),
    xTrace.item({ sequence: 1, role: 'assistant', part: xTrace.text('work a') }),
    xTrace.item({ sequence: 2, role: 'assistant', part: xTrace.text('work b') }),
    xTrace.item({ sequence: 3, role: 'assistant', part: xTrace.text('work c') }),
  ]

  // Y 已消化到 cursor 3：gap 只剩 [3, 4)
  const rendered = lifecycleWorkRecord.materialize(opening('task'), ['frame one'], trace, { Sequence: 3 }, [], OPENING_END)

  assert.match(rendered, /# Work log\nframe one/)
  assert.match(rendered, /# Uncompressed tail\nassistant: work c/)
  // 已压缩部分不重复出现于 gap
  assert.equal(rendered.includes('work a'), false)
  assert.equal(rendered.includes('work b'), false)
})

test('LWR_no_y_frames_means_opening_plus_raw_gap_not_alternate_A_path', () => {
  const trace = [
    xTrace.item({ sequence: 0, role: 'user', part: xTrace.text('task') }),
    xTrace.item({ sequence: 1, role: 'assistant', part: xTrace.text('work a') }),
  ]

  // Y 从未成功：coverage 在 origin(0)，但 gap 仍从 openingEnd 开始——同一物化
  // 算法，无「无 B 则整个 A」的旁路分支（EXEC-008、方案 4.4）
  const rendered = lifecycleWorkRecord.materialize(opening('task'), [], trace, { Sequence: 0 }, [], OPENING_END)

  assert.match(rendered, /# Uncompressed tail\nassistant: work a/)
  assert.equal(rendered.includes('# Work log'), false)
  // Opening 段一次（标题 # Opening task 不算内容）；gap 不含 opening
  assert.equal(rendered.split('\ntask\n').length - 1, 1)
})

test('LWR_terminal_output_is_byte_exact_and_not_in_a_separate_field', () => {
  const trace = [xTrace.item({ sequence: 0, role: 'user', part: xTrace.text('task') })]
  const terminal = [xTrace.item({ sequence: 1, role: 'assistant', part: xTrace.text('Final summary with detail') })]

  const rendered = lifecycleWorkRecord.materialize(opening('task'), [], trace, { Sequence: 1 }, terminal, OPENING_END)

  assert.match(rendered, /# Final output\nFinal summary with detail/)  // terminal 不是独立字段；它只出现在 LWR 段内（EXEC-006）
  assert.equal(rendered.includes('final_text'), false)
})

test('LWR_materialization_is_deterministic', () => {
  const trace = [
    xTrace.item({ sequence: 0, role: 'user', part: xTrace.text('task') }),
    xTrace.item({ sequence: 1, role: 'assistant', part: xTrace.text('work') }),
  ]

  const first = lifecycleWorkRecord.materialize(opening('task'), ['f1'], trace, { Sequence: 1 }, [], OPENING_END)
  const second = lifecycleWorkRecord.materialize(opening('task'), ['f1'], trace, { Sequence: 1 }, [], OPENING_END)
  assert.equal(first, second)
})

test('LWR_empty_sections_are_omitted', () => {
  const trace = [xTrace.item({ sequence: 0, role: 'user', part: xTrace.text('task') })]

  const rendered = lifecycleWorkRecord.materialize(opening('task'), [], trace, { Sequence: 1 }, [], OPENING_END)
  // gap 空、无 frames、无 terminal → 只有 Opening
  assert.equal(rendered.includes('# Work log'), false)
  assert.equal(rendered.includes('# Uncompressed tail'), false)
  assert.equal(rendered.includes('# Final output'), false)
  assert.equal(rendered.includes('# Opening task'), true)
})

test('LWR_child_opening_excludes_parent_work_record_envelope', () => {
  // 父 LWR 是继承 context，不复制进 child 的 Opening（EXEC-006）
  const assignment = 'child task'
  const parentEnvelope = '# parent_work_record ...'

  const rendered = lifecycleWorkRecord.materialize(opening(assignment), [], [], { Sequence: 0 }, [], OPENING_END)

  assert.equal(rendered.includes(parentEnvelope), false)
  assert.equal(rendered.includes(assignment), true)
})

test('LWR_reviewer_opening_preserves_authoritative_requirement_order', () => {
  const requirements = ['requirement one', 'requirement two', 'requirement three']

  const rendered = lifecycleWorkRecord.materialize(opening('review task', requirements), [], [], { Sequence: 0 }, [], OPENING_END)

  const oneIndex = rendered.indexOf('1. requirement one')
  const twoIndex = rendered.indexOf('2. requirement two')
  const threeIndex = rendered.indexOf('3. requirement three')
  assert.equal(oneIndex >= 0, true)
  assert.equal(twoIndex > oneIndex, true)
  assert.equal(threeIndex > twoIndex, true)
})

test('LWR_gap_starts_at_record_coverage_not_prefix_cutoff', () => {
  const trace = [
    xTrace.item({ sequence: 0, role: 'user', part: xTrace.text('task') }),
    xTrace.item({ sequence: 1, role: 'assistant', part: xTrace.text('work a') }),
    xTrace.item({ sequence: 2, role: 'assistant', part: xTrace.text('work b') }),
  ]

  // IngestedThrough 可落在 turn 中间（cursor 2）；gap 从 2 起，不含 work a
  const rendered = lifecycleWorkRecord.materialize(opening('task'), ['f1'], trace, { Sequence: 2 }, [], OPENING_END)
  assert.match(rendered, /assistant: work b/)
  assert.equal(rendered.includes('work a'), false)
})

test('LWR_gap_excludes_raw_tool_call_and_result_but_keeps_text_and_reasoning', () => {
  // COMPANION-003: tool in/out 可作 Y 压缩源，但禁止 raw 进入 LWR。
  const hugeResult = 'FILE_CONTENTS_' + 'x'.repeat(200)
  const trace = [
    xTrace.item({ sequence: 0, role: 'user', part: xTrace.text('task') }),
    xTrace.item({ sequence: 1, role: 'assistant', part: xTrace.reasoning('plan next step') }),
    xTrace.item({ sequence: 2, role: 'assistant', part: xTrace.toolCall('read', '{"path":"big.fs"}') }),
    xTrace.item({ sequence: 3, role: 'assistant', part: xTrace.toolResult(hugeResult) }),
    xTrace.item({ sequence: 4, role: 'assistant', part: xTrace.text('summarized outcome') }),
  ]

  const rendered = lifecycleWorkRecord.materialize(opening('task'), [], trace, { Sequence: 0 }, [], OPENING_END)

  assert.match(rendered, /plan next step/)
  assert.match(rendered, /assistant: summarized outcome/)
  assert.equal(rendered.includes('[tool call]'), false)
  assert.equal(rendered.includes('[tool result]'), false)
  assert.equal(rendered.includes('big.fs'), false)
  assert.equal(rendered.includes(hugeResult), false)
  assert.equal(rendered.includes('FILE_CONTENTS_'), false)
})

test('LWR_terminal_excludes_raw_tool_parts', () => {
  const terminal = [
    xTrace.item({ sequence: 1, role: 'assistant', part: xTrace.toolCall('bash', '{"command":"cat huge.log"}') }),
    xTrace.item({ sequence: 2, role: 'assistant', part: xTrace.toolResult('LOG_LINE\n'.repeat(50)) }),
    xTrace.item({ sequence: 3, role: 'assistant', part: xTrace.text('Final summary with detail') }),
    xTrace.item({ sequence: 4, role: 'assistant', part: xTrace.reasoning('closing thought') }),
  ]
  const trace = [xTrace.item({ sequence: 0, role: 'user', part: xTrace.text('task') })]

  const rendered = lifecycleWorkRecord.materialize(opening('task'), [], trace, { Sequence: 1 }, terminal, OPENING_END)

  assert.match(rendered, /# Final output\nFinal summary with detail/)
  assert.match(rendered, /closing thought/)
  assert.equal(rendered.includes('[tool call]'), false)
  assert.equal(rendered.includes('[tool result]'), false)
  assert.equal(rendered.includes('huge.log'), false)
  assert.equal(rendered.includes('LOG_LINE'), false)
})

test('LWR_parent_to_child_includes_opening', () => {
  // EXEC-006: parent → child background keeps Opening (includeOpening default true).
  const rendered = lifecycleWorkRecord.materialize(
    opening('assigned task'),
    ['did work'],
    [],
    { Sequence: 0 },
    [],
    OPENING_END,
    true,
  )

  assert.equal(rendered.includes('# Opening task'), true)
  assert.equal(rendered.includes('assigned task'), true)
  assert.match(rendered, /# Work log\ndid work/)
})

test('LWR_child_to_parent_omits_opening', () => {
  // EXEC-006: child → parent join omits Opening — assigner already knows the task.
  const rendered = lifecycleWorkRecord.materialize(
    opening('assigned task'),
    ['did work'],
    [],
    { Sequence: 0 },
    [xTrace.item({ sequence: 1, role: 'assistant', part: xTrace.text('Final summary') })],
    OPENING_END,
    false,
  )

  assert.equal(rendered.includes('# Opening task'), false)
  assert.equal(rendered.includes('assigned task'), false)
  assert.match(rendered, /# Work log\ndid work/)
  assert.match(rendered, /# Final output\nFinal summary/)
})
