// Tool contracts remain owner-defined; semantic tests do not import codec or
// runtime unions. Keep negative legacy-DTO and role boundaries observable.
import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'

const fork = readFileSync(new URL('../../../src/Wanxiangshu/Execution/Delegation/Fork/OpenCode/Tool.fs', import.meta.url), 'utf8')
const inspector = readFileSync(new URL('../../../src/Wanxiangshu/OpenCode/Tools/InspectorTool.fs', import.meta.url), 'utf8')
const coder = readFileSync(new URL('../../../src/Wanxiangshu/OpenCode/Tools/CoderTool.fs', import.meta.url), 'utf8')
test('WHAT[DELEG-019] TOOL_CONTRACT_fork_has_manager_and_orchestrator_specs', () => {
  assert.match(fork, /managerSpec/)
  assert.match(fork, /orchestratorSpec/)
})
test('WHAT[DELEG-019] TOOL_CONTRACT_inspector_and_coder_are_owner_specs', () => {
  assert.match(inspector, /module InspectorTool/)
  assert.match(coder, /establishSpec|repairSpec/)
})
test('WHAT[DELEG-019] TOOL_CONTRACT_no_legacy_dto_shape_crosses_owner_boundary', () => {
  const tag = ['.', 'tag'].join('')
  const fields = ['.', 'fields'].join('')
  const cases = ['cases', '()'].join('')
  assert.equal(inspector.includes(tag) || inspector.includes(fields) || inspector.includes(cases), false)
  assert.equal(coder.includes(tag) || coder.includes(fields) || coder.includes(cases), false)
})
