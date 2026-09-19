import assert from 'node:assert/strict'
import test from 'node:test'
import * as calibration from '../../../dist/OpenCode/Host/PairProgrammingCalibrationSurface.js'

const { renderToolEstimate } = calibration

test('WHAT[cognitive-environment-014] CE_014_tool_estimate_is_explicitly_advisory_in_both_provider_languages', () => {
  const en = renderToolEstimate('English', 4)
  assert.match(en, /4/)
  assert.match(en, /delegator|commissioner/i)
  assert.match(en, /not .*limit|not .*cap|advisory/i)
  assert.match(en, /scope|parallel|delegate|split/i)

  const zh = renderToolEstimate('SimplifiedChinese', 4)
  assert.match(zh, /4/)
  assert.match(zh, /委任|委托|估算/)
  assert.match(zh, /不是.*上限|并非.*上限|不.*限制/)
  assert.match(zh, /范围|并行|委派|分裂/)

  // Mutation test: asserting hard limit must fail
  assert.throws(() => {
    if (!/hard limit|mandatory quota/i.test(en)) {
      throw new Error('Not a hard limit')
    }
  }, /Not a hard limit/)
})
