// COMPANION-003 / EXEC-006 / EXEC-008 — LifecycleWorkRecord 物化。
//
// LWR = Opening? + Chronicle + Recent work。Closing report 已删除。
// 覆盖：byte-exact opening、最后一条助手文本在 Recent work、gap 从
// max(ingestedThrough, openingEnd) 起、无 Y 时同一算法、空段省略、determinism、
// child opening 排除 parent envelope。

import { test } from 'node:test'
import assert from 'node:assert/strict'
import { xTrace, lifecycleWorkRecord, magicTodo } from '../../verification-system/tests/support/domain.mjs'

const opening = (assignment, requirements = []) => lifecycleWorkRecord.opening({ assignment, requirements })

// 公共 fixture：trace 中 cursor 0 是 opening（Y 起点在 opening 之后，方案 4.1）
const OPENING_END = { Sequence: 1 }

test('WHAT[WORK-RECORD-008] LWR_opening_prompt_is_byte_exact_and_appears_exactly_once', () => {
  const assignment = 'Rewrite the fallback controller.\nKeep it typed.'
  const trace = [xTrace.item({ sequence: 0, role: 'user', part: xTrace.text(assignment) })]

  const rendered = lifecycleWorkRecord.materialize(opening(assignment), [], trace, { Sequence: 1 }, OPENING_END)

  assert.equal(rendered.includes('Opening'), true)
  assert.equal(rendered.includes('Opening task'), false)
  // Opening 只出现在 Opening 段，不重复于 gap（openingEnd 之后的 gap 为空）
  assert.equal(rendered.split(assignment).length - 1, 1)
  // 首条 prompt 原文逐字，不 Trim、不重排
  assert.match(rendered, new RegExp(assignment.replace(/\n/g, '\\n')))
})

test('WHAT[WORK-RECORD-003] LWR_y_frames_cover_prefix_and_x_supplies_only_suffix', () => {
  const trace = [
    xTrace.item({ sequence: 0, role: 'user', part: xTrace.text('task') }),
    xTrace.item({ sequence: 1, role: 'assistant', part: xTrace.text('work a') }),
    xTrace.item({ sequence: 2, role: 'assistant', part: xTrace.text('work b') }),
    xTrace.item({ sequence: 3, role: 'assistant', part: xTrace.text('work c') }),
  ]

  // Y 已消化到 cursor 3：gap 只剩 [3, 4)
  const rendered = lifecycleWorkRecord.materialize(opening('task'), ['frame one'], trace, { Sequence: 3 }, OPENING_END)

  assert.match(rendered, /Chronicle\nframe one/)
  assert.match(rendered, /Recent work\nassistant: work c/)
  assert.equal(rendered.includes('Work log'), false)
  assert.equal(rendered.includes('Uncompressed tail'), false)
  // 已压缩部分不重复出现于 gap
  assert.equal(rendered.includes('work a'), false)
  assert.equal(rendered.includes('work b'), false)
})

test('WHAT[WORK-RECORD-003] LWR_no_y_frames_means_opening_plus_raw_gap_not_alternate_A_path', () => {
  const trace = [
    xTrace.item({ sequence: 0, role: 'user', part: xTrace.text('task') }),
    xTrace.item({ sequence: 1, role: 'assistant', part: xTrace.text('work a') }),
  ]

  // Y 从未成功：coverage 在 origin(0)，但 gap 仍从 openingEnd 开始——同一物化
  // 算法，无「无 B 则整个 A」的旁路分支（EXEC-008、方案 4.4）
  const rendered = lifecycleWorkRecord.materialize(opening('task'), [], trace, { Sequence: 0 }, OPENING_END)

  assert.match(rendered, /Recent work\nassistant: work a/)
  assert.equal(rendered.includes('Chronicle'), false)
  // Opening 段一次（标题 Opening 不算内容）；gap 不含 opening
  assert.equal(rendered.split('\ntask\n').length - 1, 1)
})

test('WHAT[WORK-RECORD-011] LWR_last_assistant_text_is_in_recent_work_not_a_closing_report', () => {
  const trace = [
    xTrace.item({ sequence: 0, role: 'user', part: xTrace.text('task') }),
    xTrace.item({ sequence: 1, role: 'assistant', part: xTrace.text('Final summary with detail') }),
  ]

  const rendered = lifecycleWorkRecord.materialize(opening('task'), [], trace, { Sequence: 1 }, OPENING_END)

  assert.match(rendered, /Recent work\nassistant: Final summary with detail/)
  assert.equal(rendered.includes('Closing report'), false)
  assert.equal(rendered.includes('Final output'), false)
  assert.equal(rendered.includes('final_text'), false)
})


test('WHAT[WORK-RECORD-010] LWR_materialization_is_deterministic', () => {
  const trace = [
    xTrace.item({ sequence: 0, role: 'user', part: xTrace.text('task') }),
    xTrace.item({ sequence: 1, role: 'assistant', part: xTrace.text('work') }),
  ]

  const first = lifecycleWorkRecord.materialize(opening('task'), ['f1'], trace, { Sequence: 1 }, OPENING_END)
  const second = lifecycleWorkRecord.materialize(opening('task'), ['f1'], trace, { Sequence: 1 }, OPENING_END)
  assert.equal(first, second)
})

test('WHAT[WORK-RECORD-011] LWR_empty_sections_are_omitted', () => {
  const trace = [xTrace.item({ sequence: 0, role: 'user', part: xTrace.text('task') })]

  const rendered = lifecycleWorkRecord.materialize(opening('task'), [], trace, { Sequence: 1 }, OPENING_END)
  // gap 空、无 frames → 只有 Opening
  assert.equal(rendered.includes('Chronicle'), false)
  assert.equal(rendered.includes('Recent work'), false)
  assert.equal(rendered.includes('Closing report'), false)
  assert.equal(rendered.includes('Opening'), true)
  assert.equal(rendered.includes('Opening task'), false)
})

test('WHAT[WORK-RECORD-006] LWR_child_opening_excludes_parent_work_record_envelope', () => {
  // 父 LWR 是继承 context，不复制进 child 的 Opening（EXEC-006）
  const assignment = 'child task'
  const parentEnvelope = '# commissioner_record ...'

  const rendered = lifecycleWorkRecord.materialize(opening(assignment), [], [], { Sequence: 0 }, OPENING_END)

  assert.equal(rendered.includes(parentEnvelope), false)
  assert.equal(rendered.includes(assignment), true)
})

test('WHAT[WORK-RECORD-008] LWR_reviewer_opening_preserves_authoritative_requirement_order', () => {
  const requirements = ['requirement one', 'requirement two', 'requirement three']

  const rendered = lifecycleWorkRecord.materialize(opening('review task', requirements), [], [], { Sequence: 0 }, OPENING_END)

  const oneIndex = rendered.indexOf('1. requirement one')
  const twoIndex = rendered.indexOf('2. requirement two')
  const threeIndex = rendered.indexOf('3. requirement three')
  assert.equal(oneIndex >= 0, true)
  assert.equal(twoIndex > oneIndex, true)
  assert.equal(threeIndex > twoIndex, true)
})

test('WHAT[WORK-RECORD-005] LWR_gap_starts_at_record_coverage_not_prefix_cutoff', () => {
  const trace = [
    xTrace.item({ sequence: 0, role: 'user', part: xTrace.text('task') }),
    xTrace.item({ sequence: 1, role: 'assistant', part: xTrace.text('work a') }),
    xTrace.item({ sequence: 2, role: 'assistant', part: xTrace.text('work b') }),
  ]

  // IngestedThrough 可落在 turn 中间（cursor 2）；gap 从 max(IngestedThrough, openingEnd)=2 起，不含 work a
  const rendered = lifecycleWorkRecord.materialize(opening('task'), ['f1'], trace, { Sequence: 2 }, OPENING_END)
  assert.match(rendered, /assistant: work b/)
  assert.equal(rendered.includes('work a'), false)
})

test('WHAT[WORK-RECORD-014] LWR_gap_never_uses_prefix_cutoff', () => {
  const trace = [
    xTrace.item({ sequence: 0, role: 'user', part: xTrace.text('task') }),
    xTrace.item({ sequence: 1, role: 'assistant', part: xTrace.text('work a') }),
    xTrace.item({ sequence: 2, role: 'assistant', part: xTrace.text('work b') }),
  ]

  // WORK-RECORD-014：LWR gap 只消费 RecordCoverage（XTrace 游标，可落 turn 中间），
  // 绝不取 PrefixCoverage（完整 Host turn 边界）定位。cursor 1 之后是完整 turn 边界，
  // 若误用 prefix 量纲 gap 会含 work a；实际 gap 从 cursor 2 起——RecordCoverage 独一量纲。
  const rendered = lifecycleWorkRecord.materialize(opening('task'), ['f1'], trace, { Sequence: 2 }, OPENING_END)
  assert.match(rendered, /assistant: work b/)
  assert.equal(rendered.includes('work a'), false)
})

test('WHAT[WORK-RECORD-013] LWR_gap_excludes_raw_tool_call_and_result_but_keeps_text_and_reasoning', () => {
  // COMPANION-003: tool in/out 可作 Y 压缩源，但禁止 raw 进入 LWR。
  const hugeResult = 'FILE_CONTENTS_' + 'x'.repeat(200)
  const trace = [
    xTrace.item({ sequence: 0, role: 'user', part: xTrace.text('task') }),
    xTrace.item({ sequence: 1, role: 'assistant', part: xTrace.reasoning('plan next step') }),
    xTrace.item({ sequence: 2, role: 'assistant', part: xTrace.toolCall('read', '{"path":"big.fs"}') }),
    xTrace.item({ sequence: 3, role: 'assistant', part: xTrace.toolResult(hugeResult) }),
    xTrace.item({ sequence: 4, role: 'assistant', part: xTrace.text('summarized outcome') }),
  ]

  const rendered = lifecycleWorkRecord.materialize(opening('task'), [], trace, { Sequence: 0 }, OPENING_END)

  assert.match(rendered, /plan next step/)
  assert.match(rendered, /assistant: summarized outcome/)
  assert.equal(rendered.includes('[tool call]'), false)
  assert.equal(rendered.includes('[tool result]'), false)
  assert.equal(rendered.includes('big.fs'), false)
  assert.equal(rendered.includes(hugeResult), false)
  assert.equal(rendered.includes('FILE_CONTENTS_'), false)
})

test('WHAT[WORK-RECORD-013] LWR_recent_work_excludes_raw_tool_parts_and_keeps_last_assistant_text', () => {
  const trace = [
    xTrace.item({ sequence: 0, role: 'user', part: xTrace.text('task') }),
    xTrace.item({ sequence: 1, role: 'assistant', part: xTrace.toolCall('bash', '{"command":"cat huge.log"}') }),
    xTrace.item({ sequence: 2, role: 'assistant', part: xTrace.toolResult('LOG_LINE\n'.repeat(50)) }),
    xTrace.item({ sequence: 3, role: 'assistant', part: xTrace.text('Final summary with detail') }),
    xTrace.item({ sequence: 4, role: 'assistant', part: xTrace.reasoning('closing thought') }),
  ]

  const rendered = lifecycleWorkRecord.materialize(opening('task'), [], trace, { Sequence: 1 }, OPENING_END)

  assert.match(rendered, /Recent work/)
  assert.match(rendered, /Final summary with detail/)
  assert.match(rendered, /closing thought/)
  assert.equal(rendered.includes('Closing report'), false)
  assert.equal(rendered.includes('[tool call]'), false)
  assert.equal(rendered.includes('[tool result]'), false)
  assert.equal(rendered.includes('huge.log'), false)
  assert.equal(rendered.includes('LOG_LINE'), false)
})

test('WHAT[WORK-RECORD-007] LWR_parent_to_child_includes_opening', () => {
  // EXEC-006: parent → child background keeps Opening (includeOpening default true).
  const rendered = lifecycleWorkRecord.materialize(
    opening('assigned task'),
    ['did work'],
    [],
    { Sequence: 0 },
    OPENING_END,
    true,
  )

  assert.equal(rendered.includes('Opening'), true)
  assert.equal(rendered.includes('Opening task'), false)
  assert.equal(rendered.includes('assigned task'), true)
  assert.match(rendered, /Chronicle\ndid work/)
})

test('WHAT[WORK-RECORD-007] LWR_child_to_parent_omits_opening', () => {
  // EXEC-006: child → parent join omits Opening — assigner already knows the task.
  const rendered = lifecycleWorkRecord.materialize(
    opening('assigned task'),
    ['did work'],
    [xTrace.item({ sequence: 1, role: 'assistant', part: xTrace.text('Final summary') })],
    { Sequence: 0 },
    OPENING_END,
    false,
  )

  assert.equal(rendered.startsWith('Opening\n'), false)
  assert.equal(rendered.includes('assigned task'), false)
  assert.match(rendered, /Chronicle\ndid work/)
  assert.match(rendered, /Recent work/)
  assert.match(rendered, /Final summary/)
  assert.equal(rendered.includes('Closing report'), false)
})

test('WHAT[WORK-RECORD-001] LWR_same_record_projected_two_ways_shares_work_facts', () => {
  // COMPANION-015 ①：record 属于一段 work，不属于 receiver。同一 canonical record
  // 以 includeOpening=true / false 两种投影物化，work facts（Chronicle / Recent work）
  // 不变，只有 Opening 渲染段不同——投影选择不改变事实。
  const openingValue = opening('assigned task')
  const frames = ['did work']
  const trace = [xTrace.item({ sequence: 1, role: 'assistant', part: xTrace.text('Final summary') })]

  const withOpening = lifecycleWorkRecord.materialize(openingValue, frames, trace, { Sequence: 0 }, OPENING_END, true)
  const withoutOpening = lifecycleWorkRecord.materialize(openingValue, frames, trace, { Sequence: 0 }, OPENING_END, false)

  // 两投影共享 Chronicle 与 Recent work —— 同一段 work 的官方说法只有一份
  assert.match(withOpening, /Chronicle\ndid work/)
  assert.match(withoutOpening, /Chronicle\ndid work/)
  assert.match(withOpening, /Recent work\nassistant: Final summary/)
  assert.match(withoutOpening, /Recent work\nassistant: Final summary/)
  // 差异仅在 Opening 渲染段
  assert.equal(withOpening.includes('Opening'), true)
  assert.equal(withoutOpening.startsWith('Opening\n'), false)
})

test('WHAT[WORK-RECORD-009] LWR_t1_commitment_call_result_is_constitutive_opening_material', () => {
  // COMPANION-014 ⑨ / TODO-015：BlindPlan T1（第一次 accepted planComplete=true）的
  // todowrite call + canonical accepted result 是 constitutive Opening material，
  // 不得当 incidental tool 滤入 Recent work（XTrace.forOpening 保留 raw）。
  const t1Call = xTrace.item({ sequence: 1, role: 'assistant', part: xTrace.toolCall('todowrite', '{"planComplete":true}') })
  const t1Result = xTrace.item({ sequence: 2, role: 'assistant', part: xTrace.toolResult('accepted') })
  const openingWithT1 = lifecycleWorkRecord.withConstitutive(opening('charge'), [t1Call, t1Result])

  const rendered = lifecycleWorkRecord.materialize(openingWithT1, [], [], { Sequence: 0 }, OPENING_END)

  // T1 call/result 保留在 Opening（constitutive body），不因 forWorkRecord 被滤除
  assert.equal(rendered.includes('[tool call] todowrite'), true)
  assert.equal(rendered.includes('[tool result] accepted'), true)
  assert.equal(rendered.includes('Opening'), true)
  assert.equal(rendered.includes('Recent work'), false)
})

test('WHAT[WORK-RECORD-015] LWR_work_record_start_is_structural_floor_not_stage', () => {
  // TODO-001 / GLORY-006：WorkRecordStart = OpeningBoundary = Opening exclusive end，
  // 由 XTrace Opening cursor 纯推导（结构性 floor），不是 Stage fact，不读 WorkActivated。
  // opening cursor 0 → floor 1（exclusive）。
  assert.equal(magicTodo.workRecordStart(0), 1)

  // Post-T1：floor = constitutive T1 call+result 之后的 exclusive 边界（结构性推导）
  const parts = [
    { sequence: 1, kind: 'tool_call', toolCallId: 't1' },
    { sequence: 2, kind: 'tool_result', toolCallId: 't1' },
  ]
  assert.equal(magicTodo.effectiveOpeningFloor(true, true, 0, 1, 't1', 9, parts), 3)

  // Pre-T1：Opening 未关闭，floor 是动态 XTrace head（仍结构性，非 Stage）
  assert.equal(magicTodo.effectiveOpeningFloor(true, false, 0, null, null, 7, []), 7)
})
