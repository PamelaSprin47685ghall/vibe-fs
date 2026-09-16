import assert from 'node:assert/strict'
import test from 'node:test'
import * as nudge from '../../../dist/Enforcer/LatestTipNudgeSurface.js'

test('WHAT[GD-009] CTX_002_GUIDELINE_001_marker_without_nudge_is_guideline_text', () => {
  const text = nudge.renderGuidelineMarker({ tip: null, guideline: 'stay focused' })
  assert.equal(text, 'stay focused')
})

test('WHAT[GD-009] CTX_002_GUIDELINE_002_marker_with_nudge_is_one_instruction_plane', () => {
  const text = nudge.renderGuidelineMarker({ tip: 'tip = "sample"', guideline: 'stay focused' })
  assert.match(text, /tip = "sample"/)
  assert.match(text, /stay focused/)
})
