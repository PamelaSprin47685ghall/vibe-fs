import assert from 'node:assert/strict'
import test from 'node:test'
import * as contract from '../../../dist/Execution/Delegation/StructureContractSurface.js'

test('WHAT[DELEG-002] calling_names_differ_in_persona_depth_not_authority', () => {
  const fastCalling = contract.getCalling('fast-investigator')
  const deepCalling = contract.getCalling('deep-investigator')
  assert.equal(fastCalling.authority, deepCalling.authority)
  assert.notEqual(fastCalling.depth, deepCalling.depth)
})
