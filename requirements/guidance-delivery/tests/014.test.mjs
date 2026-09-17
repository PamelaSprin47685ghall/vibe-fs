import assert from 'node:assert/strict'
import test from 'node:test'
import * as calibration from '../../../dist/OpenCode/Host/PairProgrammingCalibrationSurface.js'

const { compose, renderToolEstimate } = calibration

const english = 'English'

const simplifiedChinese = 'SimplifiedChinese'

test('WHAT[GD-012] GD_012_tool_estimate_calibration_rendered_for_guideline_instruction', () => {
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
