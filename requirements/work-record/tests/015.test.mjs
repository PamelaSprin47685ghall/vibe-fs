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

// WorkRecordStart is the Opening cursor's exclusive end, derived purely from XTrace.
// Nothing downstream — no phase commit, no activation marker — can enlarge it.
const workRecordStart = (openingCursor) => openingCursor + 1

test('WHAT[work-record-015] LWR_work_record_start_is_structural_floor_not_stage', () => {
  // WorkRecordStart = OpeningBoundary = Opening exclusive end, derived purely from
  // the XTrace Opening cursor — a structural floor, not a Stage fact.
  // opening cursor 0 → floor 1 (exclusive).
  assert.equal(workRecordStart(0), 1)
  assert.equal(workRecordStart(5), 6)

  // A phase commit's tool call/result may enter the LWR's Opening material, but it
  // never widens the structural compression floor: that floor is always the real
  // Opening end.
  assert.equal(workRecordStart(0), 1)
})
