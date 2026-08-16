import assert from 'node:assert/strict'
import test from 'node:test'
import * as Strength from '../../../dist/Strength/Surface.js'

const binding = (owner, replica, decision, role = 'Coder', budget = 'K1') => Strength.runtimeBinding(owner, replica, decision, `run-${decision}`, role, budget, 65536, `sem-${decision}`, [])

test('WHAT[SPEC-INV-004] STRENGTH_014_runtime_is_owner_single_flight_and_decision_local', () => {
  const runtime = Strength.runtimeCreate()
  const first = binding('owner', 'replica-1', 'd1')
  const second = binding('owner', 'replica-2', 'd2')
  assert.equal(Strength.runtimeRegister(runtime, first).ok, true)
  const duplicateOwner = Strength.runtimeRegister(runtime, second)
  assert.equal(duplicateOwner.ok, false)
  assert.equal(duplicateOwner.error, 'OwnerAlreadyHasReplica')
  assert.equal(Strength.runtimeFindByReplica(runtime, 'replica-1').decisionId, 'd1')
  assert.equal(Strength.runtimeRetire(runtime, 'replica-1').decisionId, 'd1')
  assert.equal(Strength.runtimeFindByReplica(runtime, 'replica-1'), null)
  assert.equal(Strength.runtimeRegister(runtime, second).ok, true)
})

test('WHAT[SPEC-INV-004] STRENGTH_004_runtime_rejects_K0_and_ineligible_replica_authority', () => {
  const runtime = Strength.runtimeCreate()
  assert.equal(Strength.runtimeRegister(runtime, binding('o1', 'r1', 'd1', 'Coder', 'K0')).error, 'EmptyBudget')
  assert.equal(Strength.runtimeRegister(runtime, binding('o2', 'r2', 'd2', 'Manager', 'K1')).error, 'RoleIneligible')
})
