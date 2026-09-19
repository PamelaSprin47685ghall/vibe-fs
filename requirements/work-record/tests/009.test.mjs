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

const OPENING_END = { Sequence: 1 }

test('WHAT[work-record-009] LWR_t1_commitment_call_result_is_constitutive_opening_material', () => {
  // COMPANION-014 ⑨ / TODO-015：BlindPlan T1（第一次 accepted planComplete=true）的
  // todowrite call + canonical accepted result 是 constitutive Opening material，
  // 不得当 incidental tool 滤入 Recent work（XTrace.forOpening 保留 raw）。
  const t1Call = xTrace.item({ sequence: 1, role: 'assistant', part: xTrace.toolCall('todowrite', '{"planComplete":true}') })
  const t1Result = xTrace.item({ sequence: 2, role: 'assistant', part: xTrace.toolResult('accepted') })
  const openingWithT1 = workRecord.withConstitutive(
    opening('charge'),
    traceOwner.render(traceOwner.forOpening([t1Call, t1Result])),
  )

  const rendered = materialize(openingWithT1, [], [], { Sequence: 0 }, OPENING_END)

  // T1 call/result 保留在 Opening（constitutive body），不因 forWorkRecord 被滤除
  assert.equal(rendered.includes('[tool call] todowrite'), true)
  assert.equal(rendered.includes('[tool result] accepted'), true)
  assert.equal(rendered.includes('Opening'), true)
  assert.equal(rendered.includes('Recent work'), false)
})
