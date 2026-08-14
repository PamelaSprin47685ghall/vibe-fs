import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

import * as StrengthScope from '../../../dist/Infrastructure/OpenCode/Host/PluginStrengthScope.js'
import * as Settings from '../../../dist/Infrastructure/OpenCode/Host/StrengthSettings.js'

const packageJson = JSON.parse(readFileSync(new URL('../../../package.json', import.meta.url), 'utf8'))

const caseOf = (value) => value.cases()[value.tag]

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


test('STRENGTH_011_dry_run_is_an_explicit_non_default_host_canary_mode', () => {
  withEnv('WANXIANGSHU_STRENGTH_MODE', undefined, () => {
    assert.equal(caseOf(Settings.load().Mode), 'Shadow')
  })
  withEnv('WANXIANGSHU_STRENGTH_MODE', 'dry-run', () => {
    assert.equal(caseOf(Settings.load().Mode), 'DryRun')
  })
})

test('STRENGTH_011_dry_run_budget_defaults_to_k1_and_requires_explicit_k2_canary_opt_in', () => {
  withEnv('WANXIANGSHU_STRENGTH_DRY_RUN_BUDGET', undefined, () => {
    assert.equal(caseOf(Settings.dryRunBudget()), 'K1')
  })
  withEnv('WANXIANGSHU_STRENGTH_DRY_RUN_BUDGET', 'K2', () => {
    assert.equal(caseOf(Settings.dryRunBudget()), 'K2')
  })
  withEnv('WANXIANGSHU_STRENGTH_DRY_RUN_BUDGET', 'garbage', () => {
    assert.equal(caseOf(Settings.dryRunBudget()), 'K1', 'malformed canary budget fails safe to K1')
  })
})

test('STRENGTH_011_default_settings_are_shadow_k0_with_economic_holdout_and_no_k2_enablement', () => {
  // Default Host settings: Shadow, no cost template, canary unhealthy, 10%
  // control holdout kept. This is not a live Host canary and does not enable K2.
  withEnv('WANXIANGSHU_STRENGTH_MODE', undefined, () => {
    withCanary(undefined, () => {
      const settings = Settings.load()
      assert.equal(caseOf(settings.Mode), 'Shadow')
      assert.equal(settings.Costs, undefined)
      assert.equal(Settings.hostCanaryHealthy(), false)
      assert.equal(settings.ControlRateBasisPoints, 1000)
      assert.equal(settings.Policy.K2MinimumEvidence, 50)
      assert.ok(settings.Policy.K2Margin > settings.Policy.K1Margin)
    })
  })
})

test('STRENGTH_011_host_canary_is_bound_to_the_pinned_OpenCode_and_plugin_contract', () => {
  const expected = `opencode-ai@${packageJson.devDependencies['opencode-ai']}|@opencode-ai/plugin@${packageJson.peerDependencies['@opencode-ai/plugin']}|strength-host-canary-v1`
  assert.equal(Settings.HostCanaryFingerprint, expected)

  withCanary(undefined, () => assert.equal(Settings.hostCanaryHealthy(), false))
  withCanary('true', () => assert.equal(Settings.hostCanaryHealthy(), false))
  withCanary('pass', () => assert.equal(Settings.hostCanaryHealthy(), false))
  withCanary(Settings.HostCanaryFingerprint, () => assert.equal(Settings.hostCanaryHealthy(), true))
})

test('STRENGTH_011_process_fuse_is_first-failure-latched_and_cannot_be_cleared_by_a_session_cleanup', () => {
  const scope = StrengthScope.PluginStrengthScope_$ctor()
  assert.equal(StrengthScope.PluginStrengthScope__get_StrengthFuseReason(scope), undefined)

  StrengthScope.PluginStrengthScope__TripStrengthFuse_Z721C83C5(scope, 'projection-conflict')
  assert.equal(StrengthScope.PluginStrengthScope__get_StrengthFuseReason(scope), 'projection-conflict')

  StrengthScope.PluginStrengthScope__TripStrengthFuse_Z721C83C5(scope, 'later-noise')
  assert.equal(StrengthScope.PluginStrengthScope__get_StrengthFuseReason(scope), 'projection-conflict')

  StrengthScope.PluginStrengthScope__ClearSession_Z721C83C5(scope, 'owner')
  assert.equal(StrengthScope.PluginStrengthScope__get_StrengthFuseReason(scope), 'projection-conflict')
  StrengthScope.PluginStrengthScope__Dispose(scope)
})
