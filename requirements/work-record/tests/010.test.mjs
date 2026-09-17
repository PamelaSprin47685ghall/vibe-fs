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

test('WHAT[WORK-RECORD-010] LWR_materialization_is_deterministic', () => {
  const trace = [
    xTrace.item({ sequence: 0, role: 'user', part: xTrace.text('task') }),
    xTrace.item({ sequence: 1, role: 'assistant', part: xTrace.text('work') }),
  ]

  const first = materialize(opening('task'), ['f1'], trace, { Sequence: 1 }, OPENING_END)
  const second = materialize(opening('task'), ['f1'], trace, { Sequence: 1 }, OPENING_END)
  assert.equal(first, second)
})
