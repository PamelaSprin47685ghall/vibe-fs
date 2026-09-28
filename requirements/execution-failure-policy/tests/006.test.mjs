import assert from 'node:assert/strict'
import test from 'node:test'
import * as policy from '../../../dist/Execution/Failure/Surface.js'
import * as hooks from '../../../dist/OpenCode/Host/PluginHooksSurface.js'
import * as transaction from '../../../dist/OpenCode/Host/ChatAdmission/TransactionSurface.js'
import { input, capacityFence } from './support/policy-input.mjs'

test('WHAT[execution-failure-policy-006] policy requests exact pre-provider settlement and membrane rejects fatal before settlement', () => {
  const decision = policy.decide(input({ failure: 'LocalInvariant', phase: 'AcceptedBeforeProvider' }))
  assert.equal(decision.resolution, 'TerminalizeAcceptedPreProvider')
  assert.equal(decision.breaker.kind, 'NoBreakerTransition')
  assert.deepEqual(decision.capacitySettlement, { kind: 'ReleaseExactFence', fenceReference: capacityFence.reference })
  assert.equal(decision.fatality.kind, 'FatalAfterSettlement')
  assert.equal(hooks.hookFailurePolicy('LocalInvariant', 'SettlementIncomplete'), 'RejectFatalBeforeSettlement')
  assert.equal(hooks.hookFailurePolicy('LocalInvariant', 'ExactSettlementComplete'), 'FatalAfterSettlement')
  assert.equal(hooks.hookFailurePolicy('PersistenceUnknown', 'DurableOutcomeUnknown'), 'FatalAfterSettlement')
})

test('WHAT[execution-failure-policy-006] actual pre-provider settlement commits terminal before releasing capacity', async () => {
  const result = await transaction.preProviderSettlementScenario({
    sessionId: 'ses-fatal-settlement', physicalUserMessageId: 'msg-fatal-settlement',
    logicalRunId: 'logical-fatal-settlement', authorityRootUserMessageId: 'root-fatal-settlement',
    providerRun: 'provider-fatal-settlement', identitySeed: { participantIdentity: { selectedAgent: 'engineer' } },
  }, 'FatalMembraneInput', 'Exact')
  assert.deepEqual(result.trace.slice(-3), ['TerminalizeAccepted', 'UnbindExecution', 'ReleaseBeforeProvider'])
  assert.deepEqual(result.admission, { activeCapacity: 0, providerBinding: 0 })
  assert.equal(result.providerEffectCount, 0)
})

test.todo('WHAT[execution-failure-policy-006] GAP-120 inject NotCommitted and Unknown at actual terminal append and observe no exact fence release')
test.todo('WHAT[execution-failure-policy-006] GAP-120 same real child records settlement then fatal exit exactly once; no concatenated independent traces')
