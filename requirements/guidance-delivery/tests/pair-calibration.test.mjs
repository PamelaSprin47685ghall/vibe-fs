import assert from 'node:assert/strict'
import test from 'node:test'
import * as calibration from '../../../dist/OpenCode/Host/PairProgrammingCalibrationSurface.js'

const { compose, renderToolEstimate } = calibration
const english = 'English'
const simplifiedChinese = 'SimplifiedChinese'
test('WHAT[GD-012] GD_012_DELEG_022_no_estimate_means_no_dynamic_fragment', () => {
  const guideline = 'canonical pair guideline'
  assert.equal(compose(undefined, undefined, guideline), '# canonical pair guideline\n')
  assert.equal(compose('tip guidance', undefined, guideline), '# tip guidance\n# canonical pair guideline\n')
})

test('WHAT[COGNITIVE-ENVIRONMENT-014] CE_014_tool_estimate_is_explicitly_advisory_in_both_provider_languages', () => {
  const en = renderToolEstimate(english, 4)
  assert.match(en, /4/)
  assert.match(en, /delegator|commissioner/i)
  assert.match(en, /not .*limit|not .*cap|advisory/i)
  assert.match(en, /scope|parallel|delegate|split/i)

  const zh = renderToolEstimate(simplifiedChinese, 4)
  assert.match(zh, /4/)
  assert.match(zh, /委任|委托|估算/)
  assert.match(zh, /不是.*上限|并非.*上限|不.*限制/)
  assert.match(zh, /范围|并行|委派|分裂/)
})

test('WHAT[GD-012] GD_012_each_new_occurrence_can_render_a_new_remaining_without_rewriting_old_text', () => {
  const guideline = 'canonical pair guideline'
  const oldMarker = compose(undefined, renderToolEstimate(english, 3), guideline)
  const newMarker = compose(undefined, renderToolEstimate(english, 0), guideline)

  assert.match(oldMarker, /3/)
  assert.match(newMarker, /0/)
  assert.notEqual(newMarker, oldMarker)
  assert.match(oldMarker, /3/, 'the previously materialized string remains unchanged')
})

test('WHAT[GD-012] GD_012_dynamic_fragment_is_between_tip_and_guideline_in_instruction_plane', () => {
  const tip = 'tip guidance'
  const estimate = renderToolEstimate(english, 2)
  const guideline = 'canonical pair guideline'
  const marker = compose(tip, estimate, guideline)

  assert.ok(marker.indexOf('# tip guidance') < marker.indexOf(`# ${estimate}`))
  assert.ok(marker.indexOf(`# ${estimate}`) < marker.indexOf('# canonical pair guideline'))
  assert.equal(marker.split('\n').filter(Boolean).every((line) => line.startsWith('# ')), true)
})
