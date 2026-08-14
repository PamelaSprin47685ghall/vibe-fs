import assert from 'node:assert/strict'
import test from 'node:test'

import { providerLanguage } from '../../verification-system/tests/support/domain.mjs'

const {
  PairProgrammingCalibration_compose: compose,
  PairProgrammingCalibration_renderToolEstimate: renderToolEstimate,
} = await import('../../../dist/Infrastructure/OpenCode/Host/PairProgrammingCalibration.js')

test('GD_012_DELEG_022_no_estimate_means_no_dynamic_fragment', () => {
  const guideline = 'canonical pair guideline'
  assert.equal(compose(undefined, undefined, guideline), guideline)
  assert.equal(compose('tip guidance', undefined, guideline), `tip guidance\n\n${guideline}`)
})

test('CE_014_tool_estimate_is_explicitly_advisory_in_both_provider_languages', () => {
  const en = renderToolEstimate(providerLanguage.english, 4)
  assert.match(en, /4/)
  assert.match(en, /delegator|commissioner/i)
  assert.match(en, /not .*limit|not .*cap|advisory/i)
  assert.match(en, /scope|parallel|delegate|split/i)

  const zh = renderToolEstimate(providerLanguage.simplifiedChinese, 4)
  assert.match(zh, /4/)
  assert.match(zh, /委任|委托|估算/)
  assert.match(zh, /不是.*上限|并非.*上限|不.*限制/)
  assert.match(zh, /范围|并行|委派|分裂/)
})

test('GD_012_each_new_occurrence_can_render_a_new_remaining_without_rewriting_old_text', () => {
  const guideline = 'canonical pair guideline'
  const oldMarker = compose(undefined, renderToolEstimate(providerLanguage.english, 3), guideline)
  const newMarker = compose(undefined, renderToolEstimate(providerLanguage.english, 0), guideline)

  assert.match(oldMarker, /3/)
  assert.match(newMarker, /0/)
  assert.notEqual(newMarker, oldMarker)
  assert.match(oldMarker, /3/, 'the previously materialized string remains unchanged')
})

test('GD_012_dynamic_fragment_is_between_tip_and_canonical_guideline', () => {
  const tip = 'tip guidance'
  const estimate = renderToolEstimate(providerLanguage.english, 2)
  const guideline = 'canonical pair guideline'
  const marker = compose(tip, estimate, guideline)

  assert.equal(marker, `${tip}\n\n${estimate}\n\n${guideline}`)
})
