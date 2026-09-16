import assert from 'node:assert/strict'
import test from 'node:test'
import * as contract from '../../../dist/Execution/Delegation/StructureContractSurface.js'

test('WHAT[DELEG-007] sync_delegate_edges_are_the_allowed_dag_only', () => {
  assert.equal(contract.isAllowedSyncEdge('Manager', 'Inspector'), true)
  assert.equal(contract.isAllowedSyncEdge('Manager', 'Coder'), true)
  assert.equal(contract.isAllowedSyncEdge('Inspector', 'Manager'), false)
})
