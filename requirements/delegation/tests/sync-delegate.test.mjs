// SyncDelegate vocabulary through the delegation-owned surface.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as sync from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'

test('WHAT[DELEG-010] EXEC_026_tier_is_an_ignored_compat_param', () => {
  for (const tier of ['Fast', 'Deep']) {
    const value = sync.vocabulary('Inspector', tier, 'owner-reuse-scope')
    assert.equal(value.agent, 'inspector')
    assert.equal(value.role, 'inspector')
  }
})
test('WHAT[DELEG-010] EXEC_026_agentNameFor_returns_bare_inspector_coder', () => {
  assert.equal(sync.vocabulary('Inspector', 'Fast', 's').agent, 'inspector')
  assert.equal(sync.vocabulary('Inspector', 'Deep', 's').agent, 'inspector')
  assert.equal(sync.vocabulary('Coder', 'Fast', 's').agent, 'coder')
  assert.equal(sync.vocabulary('Coder', 'Deep', 's').agent, 'coder')
})
test('WHAT[DELEG-010] EXEC_026_ReuseScopeId_create_value_and_equals', () => {
  assert.equal(sync.vocabulary('Inspector', 'Fast', 'owner-reuse-scope').scope, 'owner-reuse-scope')
  assert.equal(sync.vocabulary('Inspector', 'Fast', 'other-scope').scope, 'other-scope')
})
test('WHAT[DELEG-010] EXEC_026_DedicatedDelegateKey_binds_scope_and_role', () => {
  assert.equal(sync.vocabulary('Inspector', 'Fast', 'scope-1').role, 'inspector')
  assert.equal(sync.vocabulary('Coder', 'Fast', 'scope-1').role, 'coder')
})
