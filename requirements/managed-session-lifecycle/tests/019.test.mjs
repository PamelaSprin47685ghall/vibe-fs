import assert from 'node:assert/strict'
import test from 'node:test'
import * as recovery from '../../../dist/Execution/Session/ChatExecution/RecoveryRuntimeSurface.js'



test('WHAT[managed-session-lifecycle-019] recovery interpreter requests exact reconciliation for held terminal resources', async () => {
  const result = await recovery.recoverScenarios([
    'TerminalResourceHeld',
    'TerminalResourceReleased',
  ])

  assert.deepEqual(result.decisions, ['ReconcilePhysical', 'Ignore'])
  assert.deepEqual(result.effects, ['ReconcilePhysical:ReleaseTerminalResource'])
})

test.todo('WHAT[managed-session-lifecycle-019] actual cancel and delete await every exact terminal and capacity release (GAP-126)')
