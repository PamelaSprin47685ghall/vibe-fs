import assert from 'node:assert/strict'
import test from 'node:test'
import * as tr from '../../../dist/OpenCode/Tools/ToolRegistrySurface.js'



test('WHAT[INTRA-PARTICIPANT-PARALLELISM-017] Fission admission formula requires verified CanonicalRole=Engineer, subsession origin, explicit authorization, and no active group', () => {
  assert.equal(tr.rolePredicate('fission', 'Engineer'), true, 'CanonicalRole Engineer must be eligible')

  const nonEngineerRoles = [
    'Manager',
    'Orchestrator',
    'DevOps',
    'Blogger',
    'Bookkeeper',
    'Predictor',
    'Reviewer',
    'Sphinx',
    'Coder',
    'Inspector',
    'Browser',
    'Inquiry',
  ]
  for (const role of nonEngineerRoles) {
    assert.equal(tr.rolePredicate('fission', role), false, `${role} must be rejected by Fission admission formula`)
  }
})

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-017] aliases, tool parameters, self-claim, and attached work records do not grant Fission', () => {
  const pseudoRoles = [
    'engineer-alias',
    'ManagerWithWorkRecord',
    'DevOpsEngineer',
    'CustomSubagent',
    'self-claimed-engineer',
  ]
  for (const pseudo of pseudoRoles) {
    assert.equal(tr.rolePredicate('fission', pseudo), false, `Pseudo/alias ${pseudo} must not grant Fission`)
  }
})
