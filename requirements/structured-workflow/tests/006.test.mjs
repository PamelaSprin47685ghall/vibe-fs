import assert from 'node:assert/strict'
import test from 'node:test'
import * as tr from '../../../dist/OpenCode/Tools/ToolRegistrySurface.js'



test('WHAT[structured-workflow-006] SuicideTool admission gate requires OfficeRole and admits Manager while rejecting others', () => {
  assert.equal(tr.admissionAuthority('suicide'), 'office')
  assert.equal(tr.rolePredicate('suicide', 'Manager'), true)
  assert.equal(tr.rolePredicate('suicide', 'Coder'), false)
  assert.equal(tr.rolePredicate('suicide', 'Orchestrator'), false)
  assert.equal(tr.rolePredicate('suicide', 'Inspector'), false)
})

test.todo('WHAT[structured-workflow-006] actual retirement freezes admissions and drains resources before completing its owning workflow')
