import assert from 'node:assert/strict'
import test from 'node:test'
import * as pc from '../../../dist/Enforcer/PairCalibrationSurface.js'
import * as pairProgrammingCalibrationSurface from '../../../dist/OpenCode/Host/PairProgrammingCalibrationSurface.js'

test('WHAT[GD-012] GD_012_DELEG_022_no_estimate_means_no_dynamic_fragment', () => {
  const res = pc.renderDynamicFragment({ expectedToolCalls: null, elapsedMs: 100 })
  assert.equal(res.hasEstimate, false)
  assert.doesNotMatch(res.text, /expected_tool_calls/)
})

test('WHAT[COGNITIVE-ENVIRONMENT-014] CE_014_tool_estimate_is_explicitly_advisory_in_both_provider_languages', () => {
  const en = pc.renderEstimateNotice('English', 5)
  const zh = pc.renderEstimateNotice('SimplifiedChinese', 5)
  assert.match(en, /advisory|estimate/i)
  assert.match(zh, /参考|预估/)
})

test('WHAT[GD-012] GD_012_each_new_occurrence_can_render_a_new_remaining_without_rewriting_old_text', () => {
  const occ1 = pc.materializeGuidelinePair({ ordinal: 1, remaining: 5 })
  const occ2 = pc.materializeGuidelinePair({ ordinal: 2, remaining: 4 })
  assert.match(occ1.markerText, /5/)
  assert.match(occ2.markerText, /4/)
  assert.notEqual(occ1.markerText, occ2.markerText)
})

test('WHAT[GD-012] GD_012_dynamic_fragment_is_between_tip_and_guideline_in_instruction_plane', () => {
  const full = pc.assembleInstructionPlane({
    tip: 'TIP_TEXT',
    dynamicFragment: 'DYNAMIC_TEXT',
    guideline: 'GUIDELINE_TEXT',
  })
  const tipIdx = full.indexOf('TIP_TEXT')
  const dynIdx = full.indexOf('DYNAMIC_TEXT')
  const guideIdx = full.indexOf('GUIDELINE_TEXT')
  assert.ok(tipIdx < dynIdx)
  assert.ok(dynIdx < guideIdx)
})
