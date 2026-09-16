import assert from 'node:assert/strict'
import test from 'node:test'

const {
  permissionLabels,
  executionToolName,
  contract,
} = await import('../../../dist/OpenCode/Tools/DistillationSurface.js')

test('WHAT[DISTILL-010] distiller_carries_no_execution_or_judgement_permissions_and_run_is_the_only_execution_surface', () => {
  assert.deepEqual(permissionLabels, [], 'Distiller must carry zero tool permissions')
  assert.deepEqual(contract.permissions, [])
  assert.equal(executionToolName, 'run', 'the execution tool surface is `run`; distill is not a separate provider tool')
  assert.equal(contract.executionTool, 'run')
})
