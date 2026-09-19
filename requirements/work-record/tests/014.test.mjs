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

test('WHAT[work-record-014] LWR_gap_never_uses_prefix_cutoff', () => {
  const trace = [
    xTrace.item({ sequence: 0, role: 'user', part: xTrace.text('task') }),
    xTrace.item({ sequence: 1, role: 'assistant', part: xTrace.text('work a') }),
    xTrace.item({ sequence: 2, role: 'assistant', part: xTrace.text('work b') }),
  ]

  // work-record-014：LWR gap 只消费 RecordCoverage（XTrace 游标，可落 turn 中间），
  // 绝不取 PrefixCoverage（完整 Host turn 边界）定位。cursor 1 之后是完整 turn 边界，
  // 若误用 prefix 量纲 gap 会含 work a；实际 gap 从 cursor 2 起——RecordCoverage 独一量纲。
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

test('WHAT[work-record-014] LWR_recent_work_can_start_mid_turn_at_record_coverage', () => {
  // RecordCoverage consumed through cursor 1 (the reasoning part) — mid-turn
  // relative to any complete-turn boundary. The gap must start at cursor 2.
  const rendered = materialize(
    opening('Charge'),
    [],
    trace,
    { Sequence: 2 },
    { Sequence: 1 },
    true,
  )

  // The gap holds only the suffix from cursor 2: the assistant text. Reasoning at
  // cursor 1 was consumed by Y and is not re-rendered.
  assert.ok(rendered.includes('delivered'))
  assert.ok(!rendered.includes('thinking'), 'consumed reasoning must not reappear in the gap')

  // This mid-turn position is legal as review evidence — the record does not
  // demand a complete-turn boundary. Prefix replaceability is a different
  // dimension owned by context-compression / prefix-stability.
  const recentStart = rendered.indexOf('Recent work')
  assert.ok(recentStart >= 0)
  const recent = rendered.slice(recentStart)
  assert.ok(recent.includes('delivered'))
  assert.equal((recent.match(/delivered/g) ?? []).length, 1, 'the statement appears exactly once')
})
}
