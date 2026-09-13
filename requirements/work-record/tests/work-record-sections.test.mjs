// COMPANION-003 / §18 — WorkRecord exposes exactly three canonical section headings.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as workRecord from '../../../dist/Mission/WorkRecord/OpeningSemanticSurface.js'

test('WHAT[WORK-RECORD-011] WORK_RECORD_SECTIONS_lifecycle_source_declares_three_canonical_headings', () => {
  const opening = workRecord.opening('Fix the bug', ['Must be tested', 'Must be performant'], 'Plan for the mission')
  const frames = ['Frame 1 chronicle entry', 'Frame 2 chronicle entry']
  const gap = 'Recent edits performed in this round'

  const rendered = workRecord.materialize(opening, frames, gap, true)

  // Verify three canonical section headings exist in exact structure
  assert.match(rendered, /^Opening\n/)
  assert.match(rendered, /\n\nChronicle\n/)
  assert.match(rendered, /\n\nRecent work\n/)

  // Legacy headings must not appear
  for (const legacy of ['Opening task', 'Work log', 'Uncompressed tail', 'Final output', 'Closing report']) {
    assert.equal(rendered.includes(legacy), false, `legacy heading must not appear: ${legacy}`)
  }

  const renderedEmpty = workRecord.materialize(opening, [], '', true)
  assert.match(renderedEmpty, /^Opening\n/)
  assert.equal(renderedEmpty.includes('Chronicle'), false, 'empty Chronicle section must be omitted')
  assert.equal(renderedEmpty.includes('Recent work'), false, 'empty Recent work section must be omitted')
})
