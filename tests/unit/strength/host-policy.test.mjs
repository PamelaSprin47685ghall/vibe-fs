import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

import * as Scope from '../../../dist/Infrastructure/OpenCode/Host/PluginRuntimeScope.js'
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

test('STRENGTH_011_host_canary_is_bound_to_the_pinned_OpenCode_and_plugin_contract', () => {
  const expected = `opencode-ai@${packageJson.devDependencies['opencode-ai']}|@opencode-ai/plugin@${packageJson.peerDependencies['@opencode-ai/plugin']}|strength-host-canary-v1`
  assert.equal(Settings.HostCanaryFingerprint, expected)

  withCanary(undefined, () => assert.equal(Settings.hostCanaryHealthy(), false))
  withCanary('true', () => assert.equal(Settings.hostCanaryHealthy(), false))
  withCanary('pass', () => assert.equal(Settings.hostCanaryHealthy(), false))
  withCanary(Settings.HostCanaryFingerprint, () => assert.equal(Settings.hostCanaryHealthy(), true))
})

test('STRENGTH_011_process_fuse_is_first-failure-latched_and_cannot_be_cleared_by_a_session_cleanup', () => {
  const scope = Scope.PluginRuntimeScope_$ctor_Z47771AD0(undefined)
  assert.equal(Scope.PluginRuntimeScope__get_StrengthFuseReason(scope), undefined)

  Scope.PluginRuntimeScope__TripStrengthFuse_Z721C83C5(scope, 'projection-conflict')
  assert.equal(Scope.PluginRuntimeScope__get_StrengthFuseReason(scope), 'projection-conflict')

  Scope.PluginRuntimeScope__TripStrengthFuse_Z721C83C5(scope, 'later-noise')
  assert.equal(Scope.PluginRuntimeScope__get_StrengthFuseReason(scope), 'projection-conflict')

  Scope.PluginRuntimeScope__DisposeSession_Z721C83C5(scope, 'owner')
  assert.equal(Scope.PluginRuntimeScope__get_StrengthFuseReason(scope), 'projection-conflict')
  Scope.PluginRuntimeScope__Dispose(scope)
})
