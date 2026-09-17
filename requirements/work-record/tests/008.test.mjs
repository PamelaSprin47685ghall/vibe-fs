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

test('WHAT[WORK-RECORD-008] LWR_opening_prompt_is_byte_exact_and_appears_exactly_once', () => {
  const assignment = 'Rewrite the fallback controller.\nKeep it typed.'
  const trace = [xTrace.item({ sequence: 0, role: 'user', part: xTrace.text(assignment) })]

  const rendered = materialize(opening(assignment), [], trace, { Sequence: 1 }, OPENING_END)

  assert.equal(rendered.includes('Opening'), true)
  assert.equal(rendered.includes('Opening task'), false)
  // Opening 只出现在 Opening 段，不重复于 gap（openingEnd 之后的 gap 为空）
  assert.equal(rendered.split(assignment).length - 1, 1)
  // 首条 prompt 原文逐字，不 Trim、不重排
  assert.match(rendered, new RegExp(assignment.replace(/\n/g, '\\n')))
})

test('WHAT[WORK-RECORD-008] LWR_reviewer_opening_preserves_authoritative_requirement_order', () => {
  const requirements = ['requirement one', 'requirement two', 'requirement three']

  const rendered = materialize(opening('review task', requirements), [], [], { Sequence: 0 }, OPENING_END)

  const oneIndex = rendered.indexOf('1. requirement one')
  const twoIndex = rendered.indexOf('2. requirement two')
  const threeIndex = rendered.indexOf('3. requirement three')
  assert.equal(oneIndex >= 0, true)
  assert.equal(twoIndex > oneIndex, true)
  assert.equal(threeIndex > twoIndex, true)
})
