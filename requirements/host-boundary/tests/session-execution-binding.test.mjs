import assert from 'node:assert/strict'
import test from 'node:test'
import * as binding from '../../../dist/OpenCode/Host/SessionBindingSurface.js'
import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'
import { runListenerRefcountScenario } from './support/listener-refcount.mjs'

await routing.initialize()

const model = { providerID: 'openai', modelID: 'gpt-5' }

test('WHAT[HOST-BOUNDARY-006] HOST-006_user_facing_agent_is_not_session_authority', () => {
  binding.drop('ses_binding_1')
  binding.observeUserFacingAgent('ses_binding_1', 'fast-coder')
  const prepared = binding.prepareUserFacing('ses_binding_1', 'fast-coder', false, model)
  assert.equal(prepared.ok, true)
  assert.equal(prepared.value.agent, 'fast-coder')
  assert.equal(binding.tryAgent('ses_binding_1'), 'fast-coder')
})

test('WHAT[HOST-BOUNDARY-006] HOST-006_accept_prompt_execution_binds_physical_prompt_and_provider_model', () => {
  binding.drop('ses_binding_2')
  binding.acceptPromptExecution('ses_binding_2', 'prompt-1', 'physical-1', 'deep-coder', model)
  const began = binding.beginProviderAttempt('ses_binding_2', 'physical-1', 'prompt-1')
  assert.equal(began.ok, true)
  const allowed = binding.validateObservedProvider('ses_binding_2', 'deep-coder', model)
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

test('WHAT[HOST-BOUNDARY-006] HOST-006_managed_prompt_preserves_agent_but_does_not_acquire_model', () => {
  binding.drop('ses_binding_4')
  const prepared = binding.prepareManaged('ses_binding_4', 'deep-coder', false, model)
  assert.equal(prepared.ok, true)
  assert.equal(prepared.value.agent, 'deep-coder')
  assert.equal(prepared.value.modelProvided, true)
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
