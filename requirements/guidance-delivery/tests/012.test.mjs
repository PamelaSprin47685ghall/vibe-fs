import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import * as calibration from '../../../dist/OpenCode/Host/PairProgrammingCalibrationSurface.js'

const comment = (text) => text.trim().split('\n').map((line) => line === '' ? '#' : `# ${line}`).join('\n') + '\n'

test('WHAT[guidance-delivery-012] actual composition preserves all provided dynamic fragments and omits absent ones', () => {
  const fragments = ['tip identity', 'elapsed calibration', 'remaining estimate', 'guideline body']
  assert.equal(calibration.composeWithElapsed(...fragments), comment(fragments.join('\n')))
  assert.equal(calibration.compose(undefined, undefined, fragments[3]), comment(fragments[3]))
})

for (const [locale, resource] of [['English', 'en'], ['SimplifiedChinese', 'zh-CN']]) {
  test(`WHAT[guidance-delivery-012] ${locale} estimate uses complete authored calibration with the current value`, () => {
    const template = readFileSync(new URL(`../../../resources/provider/host/pair-programming-tool-estimate/${resource}.md`, import.meta.url), 'utf8').trim()
    for (const remaining of [0, 4, 17]) {
      assert.equal(calibration.renderToolEstimate(locale, remaining), template.replaceAll('{{remaining}}', String(remaining)))
    }
  })
}

test.todo('WHAT[guidance-delivery-012] GAP-116 actual new occurrence reads each dynamic owner once and freezes concern consumption atomically; replay reads none')
