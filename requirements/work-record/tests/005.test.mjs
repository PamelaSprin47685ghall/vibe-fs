import test from 'node:test'
import assert from 'node:assert/strict'
import * as todo from '../../../dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js'
import * as workRecord from '../../../dist/Mission/WorkRecord/OpeningSemanticSurface.js'
import * as traceOwner from '../../../dist/Context/Trace/SemanticTraceSurface.js'

// COMPANION-003 / EXEC-006 / EXEC-008 — LifecycleWorkRecord 物化。
//
// LWR = Opening? + Chronicle + Recent work。Closing report 已删除。
// 覆盖：byte-exact opening、最后一条助手文本在 Recent work、gap 从
// max(ingestedThrough, openingEnd) 起、无 Y 时同一算法、空段省略、determinism、
// child opening 排除 parent envelope。

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

// WORK-RECORD-014 — RecordCoverage ≠ PrefixCoverage, two proof dimensions.
//
// The LWR gap is positioned by RecordCoverage (an XTrace cursor that may sit
// MID-turn); that same position is never a prefix-replacement proof, which
// lives in PrefixCoverage (complete Host turn boundary only). This file pins
// the record side: a mid-turn ingest is legal, self-contained review evidence,
// and the rendered Recent work reflects exactly the uncovered suffix.

// One full turn of two parts: user text at 0, assistant reasoning at 1, and a
// second turn: assistant text at 2.
const trace = [
  xTrace.item({ sequence: 0, role: 'user', part: xTrace.text('Charge') }),
  xTrace.item({ sequence: 1, role: 'assistant', part: xTrace.reasoning('thinking') }),
  xTrace.item({ sequence: 2, role: 'assistant', part: xTrace.text('delivered') }),
]

test('WHAT[WORK-RECORD-005] LWR_gap_starts_at_record_coverage_not_prefix_cutoff', () => {
  const trace = [
    xTrace.item({ sequence: 0, role: 'user', part: xTrace.text('task') }),
    xTrace.item({ sequence: 1, role: 'assistant', part: xTrace.text('work a') }),
    xTrace.item({ sequence: 2, role: 'assistant', part: xTrace.text('work b') }),
  ]

  // IngestedThrough 可落在 turn 中间（cursor 2）；gap 从 max(IngestedThrough, openingEnd)=2 起，不含 work a
  const rendered = materialize(opening('task'), ['f1'], trace, { Sequence: 2 }, OPENING_END)
  assert.match(rendered, /assistant: work b/)
  assert.equal(rendered.includes('work a'), false)
})

test('WHAT[WORK-RECORD-005] LWR_gap_from_origin_is_full_history_including_partial_turn', () => {
  // With coverage at origin, the gap is the whole trace after the opening end —
  // still NOT turn-bounded: a partial turn is a valid uncovered suffix.
  const rendered = materialize(
    opening('Charge'),
    [],
    trace,
    { Sequence: 0 },
    { Sequence: 1 },
    true,
  )

  // WORK-RECORD-005：Recent work = bounded invocation 内 Y 未覆盖的 X-derived suffix，
  // 不是「最近发生的事」。coverage 在 origin 时 suffix 就是全部历史——包括 partial turn。
  assert.ok(rendered.includes('thinking'))
  assert.ok(rendered.includes('delivered'))
})
