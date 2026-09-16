import assert from 'node:assert/strict'
import test from 'node:test'
import * as contract from '../../../dist/Execution/Delegation/StructureContractSurface.js'

test('WHAT[DELEG-004] commission_and_fork_are_distinct_contracts_not_witness', () => {
  assert.equal(contract.isDistinctContract('commission', 'fork'), true)
  assert.equal(contract.isWitnessContract('commission'), false)
})
