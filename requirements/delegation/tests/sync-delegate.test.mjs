// SyncDelegate vocabulary through the delegation-owned surface.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as sync from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'

test('WHAT[DELEG-010] EXEC_026_tier_is_an_ignored_compat_param', () => {
  for (const tier of ['Fast', 'Deep']) {
    const value = sync.vocabulary('Engineer', tier, 'owner-reuse-scope')
    assert.equal(value.agent, 'engineer')
    assert.equal(value.role, 'engineer')
  }
})
test('WHAT[DELEG-010] EXEC_026_agentNameFor_returns_bare_inspector_coder', () => {
  assert.equal(sync.vocabulary('Engineer', 'Fast', 's').agent, 'engineer')
  assert.equal(sync.vocabulary('Engineer', 'Deep', 's').agent, 'engineer')
  assert.equal(sync.vocabulary('Coder', 'Fast', 's').agent, 'coder')
  assert.equal(sync.vocabulary('Coder', 'Deep', 's').agent, 'coder')
})
test('WHAT[DELEG-010] EXEC_026_ReuseScopeId_create_value_and_equals', () => {
  assert.equal(sync.vocabulary('Engineer', 'Fast', 'owner-reuse-scope').scope, 'owner-reuse-scope')
  assert.equal(sync.vocabulary('Engineer', 'Fast', 'other-scope').scope, 'other-scope')
})
test('WHAT[DELEG-010] EXEC_026_DedicatedDelegateKey_binds_scope_and_role', () => {
  assert.equal(sync.vocabulary('Engineer', 'Fast', 'scope-1').role, 'engineer')
  assert.equal(sync.vocabulary('Coder', 'Fast', 'scope-1').role, 'coder')
})
