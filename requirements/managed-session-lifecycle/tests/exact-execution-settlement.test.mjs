import assert from 'node:assert/strict'
import test from 'node:test'

import * as recovery from '../../../dist/Execution/Session/ChatExecution/RecoveryRuntimeSurface.js'

test('WHAT[MANAGED-SESSION-019] cancel and delete lifecycle signals settle exact terminal resources through the execution owner', async () => {
  const signals = recovery.lifecycleSignals()
  assert.ok(signals.includes('SessionDeleted'))
  assert.ok(signals.includes('SessionCancelled'))

  const result = await recovery.recoverScenarios([
    'TerminalResourceHeld',
    'TerminalResourceReleased',
  ])

  assert.deepEqual(result.decisions, ['ReconcilePhysical', 'Ignore'])
  assert.deepEqual(result.effects, ['ReconcilePhysical:ReleaseTerminalResource'])
})
