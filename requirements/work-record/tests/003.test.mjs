// COMPANION-003 / EXEC-006 / EXEC-008 — LifecycleWorkRecord 物化。
//
// LWR = Opening? + Chronicle + Recent work。Closing report 已删除。
// 覆盖：byte-exact opening、最后一条助手文本在 Recent work、gap 从
// max(ingestedThrough, openingEnd) 起、无 Y 时同一算法、空段省略、determinism、
// child opening 排除 parent envelope。

import { test } from 'node:test'
import assert from 'node:assert/strict'
import * as todo from '../../../dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js'
import * as workRecord from '../../../dist/Mission/WorkRecord/OpeningSemanticSurface.js'
import * as traceOwner from '../../../dist/Context/Trace/SemanticTraceSurface.js'

const xTrace = {
  item: traceOwner.item,
  text: traceOwner.textPart,
  reasoning: traceOwner.reasoningPart,
  toolCall: (name, args) => traceOwner.toolCallPart('fixture-call', name, args),
  toolResult: (result) => traceOwner.toolResultPart('fixture-call', result),
}
const opening = (assignment, requirements = []) => workRecord.opening(assignment, requirements, '')
const materialize = (
  openingValue,
  frames,
  trace,
  coverage,
  openingEnd = { Sequence: 0 },
  includeOpening = true,
) => {
  const gapStart = Math.max(Number(coverage.Sequence), Number(openingEnd.Sequence))
  const gap = traceOwner.render(traceOwner.forWorkRecord(traceOwner.sliceFrom({ sequence: gapStart }, trace)))
  return workRecord.materialize(openingValue, frames, gap, includeOpening)
}

// 公共 fixture：trace 中 cursor 0 是 opening（Y 起点在 opening 之后，方案 4.1）
const OPENING_END = { Sequence: 1 }

test('WHAT[WORK-RECORD-003] LWR_y_frames_cover_prefix_and_x_supplies_only_suffix', () => {
  const trace = [
    xTrace.item({ sequence: 0, role: 'user', part: xTrace.text('task') }),
    xTrace.item({ sequence: 1, role: 'assistant', part: xTrace.text('work a') }),
    xTrace.item({ sequence: 2, role: 'assistant', part: xTrace.text('work b') }),
    xTrace.item({ sequence: 3, role: 'assistant', part: xTrace.text('work c') }),
  ]

  // Y 已消化到 cursor 3：gap 只剩 [3, 4)
  const rendered = materialize(opening('task'), ['frame one'], trace, { Sequence: 3 }, OPENING_END)

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
  const rendered = materialize(opening('task'), [], trace, { Sequence: 0 }, OPENING_END)

  assert.match(rendered, /Recent work\nassistant: work a/)
  assert.equal(rendered.includes('Chronicle'), false)
  // Opening 段一次（标题 Opening 不算内容）；gap 不含 opening
  assert.equal(rendered.split('\ntask\n').length - 1, 1)
})
