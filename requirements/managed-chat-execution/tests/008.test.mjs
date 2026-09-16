import assert from 'node:assert/strict'
import test from 'node:test'
import * as recovery from '../../../dist/Execution/Session/ChatExecution/RecoveryRuntimeSurface.js'

test('WHAT[CHATEXEC-008] recovery begins from durable activation and re-enters only on causal events', async () => {
  const [beforeDurability, afterDurability] = (await recovery.admissionCrashPointScenarios(
    ['A', 'B'],
    'ProcessRestart',
    'NotCommitted',
    'Applied',
  )).scenarios

  assert.deepEqual(beforeDurability.decisions, ['NoDurableExecution', 'NoDurableExecution'])
  assert.deepEqual(beforeDurability.effects, [])
  assert.deepEqual(afterDurability.decisions, ['ResumePreProvider', 'ResumePreProvider'])
  assert.deepEqual(afterDurability.effects, ['ResumePreProvider', 'ResumePreProvider'])
  assert.deepEqual(recovery.lifecycleSignals(), [
    'DurabilityActivated',
    'PluginRuntimeReloaded',
    'ExactAssistantStarted',
    'ExactAssistantTerminal',
    'SessionAborted',
    'SessionDeleted',
    'SessionCancelled',
    'CapacityProjectionReplayed',
  ])
})
