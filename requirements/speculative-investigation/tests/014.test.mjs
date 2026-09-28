import assert from 'node:assert/strict'
import test from 'node:test'
import * as Strength from '../../../dist/Strength/Surface.js'
import * as ablation from '../../../dist/Ablation/Surface.js'
import { withAblationEnv } from '../../feature-ablation/tests/support/ablation-fixture.mjs'

test('WHAT[speculative-investigation-014] actual Strength settings force Off for ablated mode and retain rollout choice for active and borrowed modes', t => {
  const previous = process.env.WANXIANGSHU_STRENGTH_MODE
  t.after(() => {
    if (previous === undefined) delete process.env.WANXIANGSHU_STRENGTH_MODE
    else process.env.WANXIANGSHU_STRENGTH_MODE = previous
    ablation.resetRegistry()
  })
  for (const mode of ['ablated', 'active', 'borrowed']) {
    withAblationEnv([
      ['WANXIANGSHU_ABLATION_PROFILE', mode === 'ablated' ? 'station-41' : 'production'],
      ['WANXIANGSHU_ABLATION_speculative_investigation', mode],
    ], () => {
      ablation.resetRegistry()
      assert.equal(ablation.load().ok, true, `${mode} fixture must be an admissible DAG`)
      for (const [setting, expected] of [['shadow', 'Shadow'], ['dry-run', 'DryRun'], ['treatment', 'Treatment'], ['off', 'Off']]) {
        process.env.WANXIANGSHU_STRENGTH_MODE = setting
        assert.equal(Strength.settingsLoad().mode, mode === 'ablated' ? 'Off' : expected, `${mode}/${setting}`)
      }
    })
  }
})
