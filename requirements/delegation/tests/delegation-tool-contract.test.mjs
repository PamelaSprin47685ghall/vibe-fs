// Tool contracts remain owner-defined; semantic tests do not import codec or
// runtime unions. Keep negative legacy-DTO and role boundaries observable.
import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'

const fork = readFileSync(new URL('../../../src/Wanxiangshu/Execution/Delegation/Fork/OpenCode/Tool.fs', import.meta.url), 'utf8')
const charge = readFileSync(new URL('../../../src/Wanxiangshu/Execution/Delegation/SyncDelegate/Surface.fs', import.meta.url), 'utf8')
test('WHAT[DELEG-019] TOOL_CONTRACT_fork_has_manager_and_orchestrator_specs', () => {
  assert.match(fork, /managerSpec/)
  assert.match(fork, /resumeSpec/)
  assert.match(fork, /orchestratorSpec/)
})
test('WHAT[DELEG-019] TOOL_CONTRACT_engineer_charge_is_an_owner_surface', () => {
  assert.match(charge, /executeEngineerCharge/)
})
test('WHAT[DELEG-019] TOOL_CONTRACT_no_legacy_dto_shape_crosses_owner_boundary', () => {
  const tag = ['.', 'tag'].join('')
  const fields = ['.', 'fields'].join('')
  const cases = ['cases', '()'].join('')
  assert.equal(charge.includes(tag) || charge.includes(fields) || charge.includes(cases), false)
})
