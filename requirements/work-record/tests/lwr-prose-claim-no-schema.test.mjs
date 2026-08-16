// WORK-RECORD-011 / WORK-RECORD-012 — the statement is prose, not a fixed DTO.
//
// The formal statement of a WorkRecord is the LAST assistant text in Recent work.
// There is no Closing report section and no universal report schema: rendering
// recent work must not manufacture `### Summary` / Files / Tests / Risks headings,
// and the last assistant text must appear exactly once as the final statement.

import assert from 'node:assert/strict'
import test from 'node:test'
import { xTrace, lifecycleWorkRecord } from '../../verification-system/tests/support/domain.mjs'

const opening = (assignment, requirements = []) => lifecycleWorkRecord.opening({ assignment, requirements })

const trace = [
  xTrace.item({ sequence: 0, role: 'user', part: xTrace.text('Rewrite the fallback controller.') }),
  xTrace.item({ sequence: 1, role: 'assistant', part: xTrace.reasoning('investigating') }),
  xTrace.item({ sequence: 2, role: 'assistant', part: xTrace.text('I found the root cause.') }),
  xTrace.item({ sequence: 3, role: 'assistant', part: xTrace.text('Implemented and verified the fix.') }),
]

test('WHAT[WORK-RECORD-011] LWR_statement_is_the_last_assistant_text_in_recent_work', () => {
  const rendered = lifecycleWorkRecord.materialize(
    opening('Rewrite the fallback controller.'),
    [],
    trace,
    { Sequence: 0 },
    { Sequence: 1 },
    true,
  )

  // The last assistant text appears in Recent work — not in any Closing section.
  assert.ok(rendered.includes('Implemented and verified the fix.'))
  assert.ok(rendered.includes('Recent work'))
  // No fourth section named Closing / Final output / Answer exists.
  for (const forbidden of ['Closing report', 'Final output', '## Answer', 'Closing:']) {
    assert.ok(!rendered.includes(forbidden), `no ${forbidden} section may exist`)
  }
  // The last assistant text is the final non-empty line of the record
  // (rendered with its role prefix, per XTrace.renderItem).
  const lines = rendered.split('\n').map((l) => l.trim()).filter(Boolean)
  assert.equal(lines[lines.length - 1], 'assistant: Implemented and verified the fix.')
})

test('WHAT[WORK-RECORD-012] LWR_prose_claim_never_renders_fixed_report_headings', () => {
  const rendered = lifecycleWorkRecord.materialize(
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
