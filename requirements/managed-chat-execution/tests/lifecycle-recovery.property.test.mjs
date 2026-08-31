import assert from 'node:assert/strict'
import test from 'node:test'
import * as Runtime from '../../../dist/Execution/Session/ChatExecution/RecoveryRuntimeSurface.js'

const sorted = (values) => [...values].sort()

const permutations = (values) => {
  if (values.length < 2) return [values]
  return values.flatMap((value, index) => permutations(values.filter((_, candidate) => candidate !== index)).map((rest) => [value, ...rest]))
}

test('WHAT[CHATEXEC-012] duplicate and independent permuted causal events are effect-idempotent', async () => {
  const independent = ['CrashAfterAcceptance', 'ProviderTerminalCompleted', 'TerminalResourceHeld']
  const baseline = sorted((await Runtime.recoverScenarios(independent)).effects)

  for (const order of permutations(independent)) {
    const duplicated = order.flatMap((event) => [event, event])
    assert.deepEqual(sorted((await Runtime.recoverScenarios(duplicated)).effects), baseline, order.join(','))
  }
})

test('WHAT[CHATEXEC-012] stale physical identity and stale policy evidence cannot affect a newer execution', async () => {
  const result = await Runtime.recoverScenarios(['StaleKey', 'StaleProvider', 'StalePolicy'])
  assert.deepEqual(result.effects, [])
  assert.deepEqual(result.decisions, ['Ignore', 'Ignore', 'Ignore'])
})

test('WHAT[CHATEXEC-012] restart loses only process-local deduplication and converges through idempotent owner ports', async () => {
  const result = await Runtime.recoverAcrossRestart(['AcceptedProviderAlive', 'AcceptedProviderAlive'])
  assert.deepEqual(result.effects, ['ReconcilePhysical:PersistProviderStarted'])
})
