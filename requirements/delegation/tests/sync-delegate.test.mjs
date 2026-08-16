// SyncDelegate vocabulary through the delegation-owned surface.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as sync from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'

for (const tier of ['Fast', 'Deep']) {
  test(`WHAT[DELEG-010] EXEC_026_tierForOwner_is_identity_for_${tier.toLowerCase()}`, () => {
    const value = sync.vocabulary('Inspector', tier, 'owner-reuse-scope')
    assert.equal(value.tier, tier.toLowerCase())
  })
}
test('WHAT[DELEG-010] EXEC_026_agentNameFor_covers_fast_deep_times_inspector_coder', () => {
  assert.equal(sync.vocabulary('Inspector', 'Fast', 's').agent, 'fast-inspector')
  assert.equal(sync.vocabulary('Inspector', 'Deep', 's').agent, 'deep-inspector')
  assert.equal(sync.vocabulary('Coder', 'Fast', 's').agent, 'fast-coder')
  assert.equal(sync.vocabulary('Coder', 'Deep', 's').agent, 'deep-coder')
})
test('WHAT[DELEG-010] EXEC_026_ReuseScopeId_create_value_and_equals', () => {
  assert.equal(sync.vocabulary('Inspector', 'Fast', 'owner-reuse-scope').scope, 'owner-reuse-scope')
  assert.equal(sync.vocabulary('Inspector', 'Fast', 'other-scope').scope, 'other-scope')
})
test('WHAT[DELEG-010] EXEC_026_DedicatedDelegateKey_binds_scope_and_role', () => {
  assert.equal(sync.vocabulary('Inspector', 'Fast', 'scope-1').role, 'inspector')
  assert.equal(sync.vocabulary('Coder', 'Fast', 'scope-1').role, 'coder')
})
