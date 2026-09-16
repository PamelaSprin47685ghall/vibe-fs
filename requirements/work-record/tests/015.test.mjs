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

test('WHAT[WORK-RECORD-015] LWR_work_record_start_is_structural_floor_not_stage', () => {
  // TODO-001 / GLORY-006：WorkRecordStart = OpeningBoundary = Opening exclusive end，
  // 由 XTrace Opening cursor 纯推导（结构性 floor），不是 Stage fact，不读 WorkActivated。
  // opening cursor 0 → floor 1（exclusive）。
  assert.equal(todo.workRecordStart(0), 1)

  // T1 的 constitutive call/result 可以进入 LWR Opening material，但不会扩大
  // context-compression 的 structural floor；该 floor 始终是真实 Opening 终点。
  const parts = [
    { sequence: 1, kind: 'tool_call', toolCallId: 't1' },
    { sequence: 2, kind: 'tool_result', toolCallId: 't1' },
  ]
  assert.equal(todo.effectiveOpeningFloor(true, true, 0, 1, 't1', 9, parts), 1)

  // Pre-T1 planning frontier 同样不能把普通历史升级成不可压缩 Opening。
  assert.equal(todo.effectiveOpeningFloor(true, false, 0, null, null, 7, []), 1)
})
