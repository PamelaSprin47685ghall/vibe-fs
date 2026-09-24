import { test } from 'node:test'
import assert from 'node:assert/strict'
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

const OPENING_END = { Sequence: 1 }

test('WHAT[work-record-013] LWR_gap_excludes_raw_tool_call_and_result_but_keeps_text_and_reasoning', () => {
  // COMPANION-003: tool in/out 可作 Y 压缩源，但禁止 raw 进入 LWR。
  const hugeResult = 'FILE_CONTENTS_' + 'x'.repeat(200)
  const trace = [
    xTrace.item({ sequence: 0, role: 'user', part: xTrace.text('task') }),
    xTrace.item({ sequence: 1, role: 'assistant', part: xTrace.reasoning('plan next step') }),
    xTrace.item({ sequence: 2, role: 'assistant', part: xTrace.toolCall('read', '{"path":"big.fs"}') }),
    xTrace.item({ sequence: 3, role: 'assistant', part: xTrace.toolResult(hugeResult) }),
    xTrace.item({ sequence: 4, role: 'assistant', part: xTrace.text('summarized outcome') }),
  ]

  const rendered = materialize(opening('task'), [], trace, { Sequence: 0 }, OPENING_END)

  assert.match(rendered, /plan next step/)
  assert.match(rendered, /assistant: summarized outcome/)
  assert.equal(rendered.includes('[tool call]'), false)
  assert.equal(rendered.includes('[tool result]'), false)
  assert.equal(rendered.includes('big.fs'), false)
  assert.equal(rendered.includes(hugeResult), false)
  assert.equal(rendered.includes('FILE_CONTENTS_'), false)
})

test('WHAT[work-record-013] LWR_recent_work_excludes_raw_tool_parts_and_keeps_last_assistant_text', () => {
  const trace = [
    xTrace.item({ sequence: 0, role: 'user', part: xTrace.text('task') }),
    xTrace.item({ sequence: 1, role: 'assistant', part: xTrace.toolCall('bash', '{"command":"cat huge.log"}') }),
    xTrace.item({ sequence: 2, role: 'assistant', part: xTrace.toolResult('LOG_LINE\n'.repeat(50)) }),
    xTrace.item({ sequence: 3, role: 'assistant', part: xTrace.text('Final summary with detail') }),
    xTrace.item({ sequence: 4, role: 'assistant', part: xTrace.reasoning('closing thought') }),
  ]

  const rendered = materialize(opening('task'), [], trace, { Sequence: 1 }, OPENING_END)

  assert.match(rendered, /Recent work/)
  assert.match(rendered, /Final summary with detail/)
  assert.match(rendered, /closing thought/)
  assert.equal(rendered.includes('Closing report'), false)
  assert.equal(rendered.includes('[tool call]'), false)
  assert.equal(rendered.includes('[tool result]'), false)
  assert.equal(rendered.includes('huge.log'), false)
  assert.equal(rendered.includes('LOG_LINE'), false)
})
