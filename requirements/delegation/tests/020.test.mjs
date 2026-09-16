import assert from 'node:assert/strict'
import test from 'node:test'
import * as contract from '../../../dist/Execution/Delegation/StructureContractSurface.js'

test('WHAT[DELEG-020] delegation_semantics_do_not_depend_on_current_tool_names', () => {
  assert.equal(contract.isIndependentOfToolNameLiteral(), true)
})
