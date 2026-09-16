import assert from 'node:assert/strict'
import test from 'node:test'
import * as dispatch from '../../../dist/Interaction/Dispatch/OpenCode/Surface.js'
import * as bindingRecovery from '../../../dist/Interaction/Dispatch/OpenCode/BindingRecoverySurface.js'

test('WHAT[DISPATCH-PROTOCOL-012] DP_012_physical_acceptance_hands_exact_claim_identity_to_managed_execution', () => {
  const handover = dispatch.createExecutionHandover({
    sessionId: 's-1',
    physicalMessageId: 'phys-1',
    promptKey: 'pk-1',
    participant: 'p-1',
    role: 'coder',
  })
  assert.equal(handover.sessionId, 's-1')
  assert.equal(handover.physicalMessageId, 'phys-1')
  assert.equal(handover.promptKey, 'pk-1')
  assert.equal(handover.participant, 'p-1')
  assert.equal(handover.role, 'coder')
})

test('WHAT[DISPATCH-PROTOCOL-012] recovered turn binding restores durable participant and role when process-local role is absent', () => {
  const restored = bindingRecovery.restoreBinding({
    durableParticipant: 'p-durable',
    durableRole: 'manager',
    processLocalRole: null,
  })
  assert.equal(restored.participant, 'p-durable')
  assert.equal(restored.role, 'manager')
})
