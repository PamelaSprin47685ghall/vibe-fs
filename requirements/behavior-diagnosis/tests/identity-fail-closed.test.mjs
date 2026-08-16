// Provider-run identity fail-closed boundary (ENFORCER-043).
import assert from 'node:assert/strict'
import test from 'node:test'
import * as enforcer from '../../../dist/Enforcer/Surface.js'

test('WHAT[BD-010] ENFORCER_043_no_provable_provider_run_fails_closed', () => {
  const result = enforcer.validateProviderRun('')
  assert.equal(result.ok, false)
  assert.equal(result.error, 'no provable provider run')
})

test('WHAT[BD-010] ENFORCER_043_whitespace_provider_run_fails_closed', () => {
  const result = enforcer.validateProviderRun('   ')
  assert.equal(result.ok, false)
  assert.match(result.error, /no provable provider run/)
})

test('WHAT[BD-010] ENFORCER_043_provider_run_identity_is_preserved', () => {
  const result = enforcer.validateProviderRun('asst-identity')
  assert.equal(result.ok, true)
  assert.equal(result.providerRun, 'asst-identity')
})
