import test from 'node:test'

{
const { test } = await import("node:test");
const { default: assert } = await import("node:assert/strict");
const todo = await import("../../../dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js");
const workRecord = await import("../../../dist/Mission/WorkRecord/OpeningSemanticSurface.js");
const traceOwner = await import("../../../dist/Context/Trace/SemanticTraceSurface.js");

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
const OPENING_END = { Sequence: 1 }

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
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const workRecord = await import("../../../dist/Mission/WorkRecord/OpeningSemanticSurface.js");
const traceOwner = await import("../../../dist/Context/Trace/SemanticTraceSurface.js");

const xTrace = {
  item: traceOwner.item,
  text: traceOwner.textPart,
  reasoning: traceOwner.reasoningPart,
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
const trace = [
  xTrace.item({ sequence: 0, role: 'user', part: xTrace.text('Charge') }),
  xTrace.item({ sequence: 1, role: 'assistant', part: xTrace.reasoning('thinking') }),
  xTrace.item({ sequence: 2, role: 'assistant', part: xTrace.text('delivered') }),
]

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
}
