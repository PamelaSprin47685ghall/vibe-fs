import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import * as ablation from '../../../dist/Ablation/Surface.js'

test('WHAT[SPEC-INV-014] strength is strictly forced Off when speculative-investigation is ablated prioritizing over rollout env', (t) => {
  const prevEnv = process.env.WANXIANGSHU_STRENGTH_MODE
  t.after(() => {
    if (prevEnv === undefined) {
      delete process.env.WANXIANGSHU_STRENGTH_MODE
    } else {
      process.env.WANXIANGSHU_STRENGTH_MODE = prevEnv
    }
    ablation.resetCache()
  })

  // 1. Static contract verification: Settings.fs must check AblationSettings.strengthForcedOff () before checking WANXIANGSHU_STRENGTH_MODE
  const settingsSource = readFileSync('src/Wanxiangshu/Strength/OpenCode/Settings.fs', 'utf8')
  const modeFunctionMatch = settingsSource.match(/let private mode \(\) =\s*([\s\S]*?)let private buildCosts/)
  assert.ok(modeFunctionMatch, 'mode () function must exist in Settings.fs')

  const modeBody = modeFunctionMatch[1]
  const ablationCheckIdx = modeBody.indexOf('AblationSettings.strengthForcedOff ()')
  const envCheckIdx = modeBody.indexOf('WANXIANGSHU_STRENGTH_MODE')
  assert.ok(ablationCheckIdx >= 0, 'AblationSettings.strengthForcedOff check must be present')
  assert.ok(envCheckIdx >= 0, 'WANXIANGSHU_STRENGTH_MODE check must be present')
  assert.ok(ablationCheckIdx < envCheckIdx, 'AblationSettings.strengthForcedOff must precede WANXIANGSHU_STRENGTH_MODE check')

  // 2. Dynamic check on Ablation Surface
  ablation.resetCache()
  const currentMode = ablation.modeFor('speculative-investigation')
  const isForcedOff = ablation.strengthForcedOff()
  assert.equal(isForcedOff, currentMode === 'ablated', 'strengthForcedOff must equal modeFor("speculative-investigation") === "ablated"')

  // 3. Environment isolation: setting WANXIANGSHU_STRENGTH_MODE does not bypass ablation force-off
  process.env.WANXIANGSHU_STRENGTH_MODE = 'treatment'
  assert.equal(ablation.strengthForcedOff(), currentMode === 'ablated')
})
