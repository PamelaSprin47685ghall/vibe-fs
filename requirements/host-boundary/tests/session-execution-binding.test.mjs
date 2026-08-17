import assert from 'node:assert/strict'
import test from 'node:test'
import * as binding from '../../../dist/OpenCode/Host/SessionBindingSurface.js'
import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'
import { runListenerRefcountScenario } from './support/listener-refcount.mjs'

await routing.initialize()

const model = { providerID: 'openai', modelID: 'gpt-5' }
const modelFromLease = async (sessionId, physicalUserMessageId, agent) => {
  const target = await routing.acquire(sessionId, physicalUserMessageId, agent)
  const [providerID, ...modelParts] = target.model.split('/')
  return { providerID, modelID: modelParts.join('/'), variant: target.reasoning }
}

test('WHAT[HOST-BOUNDARY-006] HOST-006_user_facing_agent_is_not_session_authority', () => {
  binding.drop('ses_binding_1')
  binding.observeUserFacingAgent('ses_binding_1', 'fast-coder')
  const prepared = binding.prepareUserFacing('ses_binding_1', 'fast-coder', false, model)
  assert.equal(prepared.ok, true)
  assert.equal(prepared.value.agent, 'fast-coder')
  assert.equal(binding.tryAgent('ses_binding_1'), 'fast-coder')
})

test('WHAT[HOST-BOUNDARY-006] HOST-006_accept_prompt_execution_binds_physical_prompt_and_provider_model', async () => {
  binding.drop('ses_binding_2')
  const leasedModel = await modelFromLease('ses_binding_2', 'physical-1', 'deep-coder')
  binding.acceptPromptExecution('ses_binding_2', 'prompt-1', 'physical-1', 'deep-coder', leasedModel)
  const began = binding.beginProviderAttempt('ses_binding_2', 'physical-1', 'prompt-1')
  assert.equal(began.ok, true)
  const allowed = binding.validateObservedProvider('ses_binding_2', 'deep-coder', leasedModel)
  assert.equal(allowed.ok, true)
  assert.equal(allowed.value, true)
})

test('WHAT[HOST-BOUNDARY-006] HOST-006_provider_drift_is_rejected_after_prompt_binding', () => {
  binding.drop('ses_binding_3')
  binding.acceptPromptExecution('ses_binding_3', 'prompt-1', 'physical-1', 'fast-coder', model)
  binding.beginProviderAttempt('ses_binding_3', 'physical-1', 'prompt-1')
  const stale = binding.validateObservedProvider('ses_binding_3', 'deep-coder', model)
  assert.equal(stale.ok, false)
  assert.match(stale.error, /provider agent drift/)
})

test('WHAT[HOST-BOUNDARY-006] HOST-006_stale_physical_terminal_cannot_strip_the_lease_before_chat_params_validation', async () => {
  const session = 'ses_binding_stale_terminal'
  binding.drop(session)

  await modelFromLease(session, 'physical-old', 'fast-coder')
  const currentModel = await modelFromLease(session, 'physical-current', 'deep-coder')

  routing.releasePhysical(session, 'physical-old')
  binding.acceptPromptExecution(session, 'prompt-current', 'physical-current', 'deep-coder', currentModel)

  const observed = binding.validateObservedProvider(session, 'deep-coder', currentModel)
  assert.equal(observed.ok, true, observed.error)
  assert.equal(observed.value, true)

  binding.drop(session)
})

test('WHAT[HOST-BOUNDARY-006] HOST-006_managed_prompt_preserves_agent_but_does_not_acquire_model', () => {
  binding.drop('ses_binding_4')
  binding.bindChild('ses_parent_4', 'ses_binding_4', 'deep-coder')
  const prepared = binding.prepareManaged('ses_binding_4', 'deep-coder', false, model)
  assert.equal(prepared.ok, true)
  assert.equal(prepared.value.agent, 'deep-coder')
  assert.equal(prepared.value.modelProvided, false)
})

test('WHAT[HOST-BOUNDARY-006] HOST-006_child_enqueue_uses_binding_agent_and_model_free_options', () => {
  const created = binding.bindChild('ses_parent', 'ses_child', 'deep-coder')
  assert.equal(created.ok, true)
  const prepared = binding.prepareManaged('ses_child', 'deep-coder', false, null)
  assert.equal(prepared.ok, true)
  assert.equal(prepared.value.agent, 'deep-coder')
  assert.equal(prepared.value.modelProvided, false)
})

test('WHAT[HOST-BOUNDARY-006] HOST-006_terminal_listener_refcounts_do_not_share_disposal', () => {
  const observed = runListenerRefcountScenario()
  assert.equal(observed.afterOneDisposeFatal, true)
  assert.equal(observed.afterAllDisposeFatal, false)
  assert.deepEqual(observed.sends, [])
})
