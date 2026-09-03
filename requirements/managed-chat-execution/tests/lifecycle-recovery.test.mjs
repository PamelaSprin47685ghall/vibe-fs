import assert from 'node:assert/strict'
import test from 'node:test'
import * as Runtime from '../../../dist/Execution/Session/ChatExecution/RecoveryRuntimeSurface.js'

test('WHAT[CHATEXEC-008] recovery begins from durable activation and re-enters only on causal events', async () => {
  const [beforeDurability, afterDurability] = (await Runtime.admissionCrashPointScenarios(
    ['A', 'B'],
    'ProcessRestart',
    'NotCommitted',
    'Applied',
  )).scenarios

  assert.deepEqual(beforeDurability.decisions, ['NoDurableExecution', 'NoDurableExecution'])
  assert.deepEqual(beforeDurability.effects, [])
  assert.deepEqual(afterDurability.decisions, ['ResumePreProvider', 'ResumePreProvider'])
  assert.deepEqual(afterDurability.effects, ['ResumePreProvider', 'ResumePreProvider'])
  assert.deepEqual(Runtime.lifecycleSignals(), [
    'DurabilityActivated',
    'PluginRuntimeReloaded',
    'ExactAssistantStarted',
    'ExactAssistantTerminal',
    'SessionAborted',
    'SessionDeleted',
    'SessionCancelled',
    'TypedFailureDecision',
    'CapacityProjectionReplayed',
  ])
})

test('WHAT[CHATEXEC-012] lifecycle recovery interprets every typed decision through its owner port', async () => {
  const cases = [
    ['ProviderAlive', 'Ignore', []],
    ['AcceptedProviderAlive', 'ReconcilePhysical', ['ReconcilePhysical:PersistProviderStarted']],
    ['CrashAfterAcceptance', 'ResumePreProvider', ['ResumePreProvider']],
    ['RetryEligible', 'RequeueEligible', ['RequeueEligible:RetryFreshAttempt']],
    ['ProviderTerminalCompleted', 'Finalize', ['Finalize:Completed']],
    ['MissingReceipt', 'MarkManualIntervention', ['MarkManualIntervention:MissingExternalReceipt']],
  ]

  for (const [scenario, decision, expected] of cases) {
    const result = await Runtime.recoverScenarios([scenario])
    assert.deepEqual(result.decisions, [decision], scenario)
    assert.deepEqual(result.effects, expected, scenario)
  }
})

test('WHAT[CHATEXEC-012] only causal lifecycle signals enter the shared recovery runtime', () => {
  assert.deepEqual(Runtime.lifecycleSignals(), [
    'DurabilityActivated',
    'PluginRuntimeReloaded',
    'ExactAssistantStarted',
    'ExactAssistantTerminal',
    'SessionAborted',
    'SessionDeleted',
    'SessionCancelled',
    'TypedFailureDecision',
    'CapacityProjectionReplayed',
  ])
})
