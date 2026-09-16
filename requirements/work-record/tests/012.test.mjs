// WORK-RECORD-011 / WORK-RECORD-012 — the statement is prose, not a fixed DTO.
//
// The formal statement of a WorkRecord is the LAST assistant text in Recent work.
// There is no Closing report section and no universal report schema: rendering
// recent work must not manufacture `### Summary` / Files / Tests / Risks headings,
// and the last assistant text must appear exactly once as the final statement.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as workRecord from '../../../dist/Mission/WorkRecord/OpeningSemanticSurface.js'
import * as traceOwner from '../../../dist/Context/Trace/SemanticTraceSurface.js'

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
  xTrace.item({ sequence: 0, role: 'user', part: xTrace.text('Rewrite the fallback controller.') }),
  xTrace.item({ sequence: 1, role: 'assistant', part: xTrace.reasoning('investigating') }),
  xTrace.item({ sequence: 2, role: 'assistant', part: xTrace.text('I found the root cause.') }),
  xTrace.item({ sequence: 3, role: 'assistant', part: xTrace.text('Implemented and verified the fix.') }),
]

test('WHAT[WORK-RECORD-012] LWR_prose_claim_never_renders_fixed_report_headings', () => {
  const rendered = materialize(
    opening('Rewrite the fallback controller.'),
    [],
    trace,
    { Sequence: 0 },
    { Sequence: 1 },
    true,
  )

  // ARCH-015: no universal report schema. Even when the work is prose-heavy, the
  // renderer must not invent Summary / Files Changed / Tests / Risks / Blockers.
  for (const forbidden of ['### Summary', '### Files', '### Tests', '### Risks', '### Blockers', 'Summary:', 'Files Changed']) {
    assert.ok(!rendered.includes(forbidden), `fixed DTO heading ${forbidden} must not appear`)
  }
})
