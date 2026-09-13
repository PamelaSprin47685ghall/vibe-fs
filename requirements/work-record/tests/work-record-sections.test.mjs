// COMPANION-003 / §18 — WorkRecord exposes exactly three canonical section headings.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as model from '../../../dist/Mission/WorkRecord/Model.js'
import { ofArray } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'

test('WHAT[WORK-RECORD-011] WORK_RECORD_SECTIONS_lifecycle_source_declares_three_canonical_headings', () => {
  const opening = {
    AssignmentText: 'Fix the bug',
    AuthoritativeRequirements: ofArray(['Must be tested', 'Must be performant']),
    ConstitutiveBody: 'Plan for the mission',
  }
  const frames = ofArray(['Frame 1 chronicle entry', 'Frame 2 chronicle entry'])
  const gap = 'Recent edits performed in this round'

  // Full render with opening
  const record = new model.LifecycleWorkRecord(opening, frames, gap)
  const rendered = model.LifecycleWorkRecordModule_render(true, record)

  // Verify three canonical section headings exist in exact structure
  assert.match(rendered, /^Opening\n/)
  assert.match(rendered, /\n\nChronicle\n/)
  assert.match(rendered, /\n\nRecent work\n/)

  // Legacy headings must not appear
  for (const legacy of ['Opening task', 'Work log', 'Uncompressed tail', 'Final output', 'Closing report']) {
    assert.equal(rendered.includes(legacy), false, `legacy heading must not appear: ${legacy}`)
  }

  // Empty sections are omitted
  const emptyFramesRecord = new model.LifecycleWorkRecord(opening, ofArray([]), '')
  const renderedEmpty = model.LifecycleWorkRecordModule_render(true, emptyFramesRecord)
  assert.match(renderedEmpty, /^Opening\n/)
  assert.equal(renderedEmpty.includes('Chronicle'), false, 'empty Chronicle section must be omitted')
  assert.equal(renderedEmpty.includes('Recent work'), false, 'empty Recent work section must be omitted')
})
