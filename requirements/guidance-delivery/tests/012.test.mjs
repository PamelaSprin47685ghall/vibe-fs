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
