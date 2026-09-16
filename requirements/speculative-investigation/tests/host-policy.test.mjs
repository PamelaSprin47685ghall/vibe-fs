import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import * as Strength from '../../../dist/Strength/Surface.js'

const packageJson = JSON.parse(readFileSync(new URL('../../../package.json', import.meta.url), 'utf8'))
const withEnv = (name, value, run) => {
  const previous = process.env[name]
  try {
    if (value === undefined) delete process.env[name]
    else process.env[name] = value
    run()
  } finally {
    if (previous === undefined) delete process.env[name]
    else process.env[name] = previous
  }
}
const withCanary = (value, run) => withEnv('WANXIANGSHU_STRENGTH_HOST_CANARY', value, run)

test('WHAT[SPEC-INV-011] STRENGTH_011_dry_run_is_an_explicit_non_default_host_canary_mode', () => {
  withEnv('WANXIANGSHU_STRENGTH_MODE', undefined, () => assert.equal(Strength.settingsLoad().mode, 'Shadow'))
  withEnv('WANXIANGSHU_STRENGTH_MODE', 'dry-run', () => assert.equal(Strength.settingsLoad().mode, 'DryRun'))
})

test('WHAT[SPEC-INV-011] STRENGTH_011_dry_run_budget_defaults_to_k1_and_requires_explicit_k2_canary_opt_in', () => {
  withEnv('WANXIANGSHU_STRENGTH_DRY_RUN_BUDGET', undefined, () => assert.equal(Strength.settingsDryRunBudget(), 'K1'))
  withEnv('WANXIANGSHU_STRENGTH_DRY_RUN_BUDGET', 'K2', () => assert.equal(Strength.settingsDryRunBudget(), 'K2'))
  withEnv('WANXIANGSHU_STRENGTH_DRY_RUN_BUDGET', 'garbage', () => assert.equal(Strength.settingsDryRunBudget(), 'K1'))
})

test('WHAT[SPEC-INV-001] STRENGTH_011_default_settings_are_shadow_k0_with_economic_holdout_and_no_k2_enablement', () => {
  withEnv('WANXIANGSHU_STRENGTH_MODE', undefined, () => {
    withCanary(undefined, () => {
      const settings = Strength.settingsLoad()
      assert.equal(settings.mode, 'Shadow')
      assert.equal(settings.costs, null)
      assert.equal(Strength.settingsHostCanaryHealthy(), false)
      assert.equal(settings.controlRateBasisPoints, 1000)
      assert.equal(settings.policy.K2MinimumEvidence, 50)
      assert.ok(settings.policy.K2Margin > settings.policy.K1Margin)
    })
  })
})

test('WHAT[SPEC-INV-011] STRENGTH_011_host_canary_is_bound_to_the_pinned_OpenCode_and_plugin_contract', () => {
  const expected = `opencode-ai@${packageJson.devDependencies['opencode-ai']}|@opencode-ai/plugin@${packageJson.peerDependencies['@opencode-ai/plugin']}|strength-host-canary-v1`
  assert.equal(Strength.settingsHostCanaryFingerprint, expected)
  withCanary(undefined, () => assert.equal(Strength.settingsHostCanaryHealthy(), false))
  withCanary('true', () => assert.equal(Strength.settingsHostCanaryHealthy(), false))
  withCanary('pass', () => assert.equal(Strength.settingsHostCanaryHealthy(), false))
  withCanary(Strength.settingsHostCanaryFingerprint, () => assert.equal(Strength.settingsHostCanaryHealthy(), true))
})

test('WHAT[SPEC-INV-011] STRENGTH_011_process_fuse_is_first-failure-latched_and_cannot_be_cleared_by_a_session_cleanup', () => {
  const scope = Strength.scopeCreate()
  assert.equal(Strength.scopeFuseReason(scope), null)
  Strength.scopeTripFuse(scope, 'projection-conflict')
  assert.equal(Strength.scopeFuseReason(scope), 'projection-conflict')
  Strength.scopeTripFuse(scope, 'later-noise')
  assert.equal(Strength.scopeFuseReason(scope), 'projection-conflict')
  Strength.scopeClearSession(scope, 'owner')
  assert.equal(Strength.scopeFuseReason(scope), 'projection-conflict')
  Strength.scopeDispose(scope)
})
