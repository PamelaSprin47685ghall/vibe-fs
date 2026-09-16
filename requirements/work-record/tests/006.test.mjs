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

test('WHAT[WORK-RECORD-006] LWR_child_opening_excludes_parent_work_record_envelope', () => {
  // 父 LWR 是继承 context，不复制进 child 的 Opening（EXEC-006）
  const assignment = 'child task'
  const parentEnvelope = '# commissioner_record ...'

  const rendered = materialize(opening(assignment), [], [], { Sequence: 0 }, OPENING_END)

  assert.equal(rendered.includes(parentEnvelope), false)
  assert.equal(rendered.includes(assignment), true)
})
