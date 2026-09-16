import assert from 'node:assert/strict'
import test from 'node:test'
import * as contract from '../../../dist/Execution/Delegation/StructureContractSurface.js'

test('WHAT[DELEG-001] manager_role_law_entrusts_by_consequence_not_persona', () => {
  const spec = contract.managerEntrustmentSpec('audit-task')
  assert.equal(spec.entrustByConsequence, true)
  assert.equal(spec.hasFixedPersona, false)
})
