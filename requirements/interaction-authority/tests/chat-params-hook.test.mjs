// INTERACTION-AUTHORITY proof — chat.params observes an existing execution binding.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as binding from '../../../dist/OpenCode/Host/SessionBindingSurface.js'
import * as chatParams from '../../../dist/OpenCode/Host/ChatParamsSurface.js'

test('WHAT[INTERACTION-AUTHORITY-011] CHAT_PARAMS_parented_session_requires_provider_model_binding', () => {
  binding.bindChild('ses_chat_params_root', 'ses_chat_params_child', 'deep-coder')
  const output = { model: { providerID: 'anthropic', modelID: 'fast-haiku' } }
  const rejected = chatParams.apply(
    { sessionID: 'ses_chat_params_child', agent: 'deep-coder', model: { providerID: 'anthropic', modelID: 'fast-haiku' } },
    output,
  )
  assert.equal(rejected.ok, false)
  assert.match(rejected.error, /no observable provider\/model binding|no exact physical execution binding/)
  assert.equal(output.model.modelID, 'fast-haiku')
})

test('WHAT[INTERACTION-AUTHORITY-011] CHAT_PARAMS_unbound_Host_auxiliary_child_does_not_claim_managed_execution', () => {
  binding.observeHostAuxiliaryChild('ses_chat_params_title')
  const output = { model: { providerID: 'anthropic', modelID: 'fast-haiku' } }
  const observed = chatParams.apply(
    { sessionID: 'ses_chat_params_title', agent: 'fast-coder', model: { providerID: 'anthropic', modelID: 'fast-haiku' } },
    output,
  )

  assert.equal(observed.ok, true, observed.error)
  assert.equal(observed.temperature, undefined)
  assert.equal(output.model.modelID, 'fast-haiku')
})

test('WHAT[INTERACTION-AUTHORITY-011] CHAT_PARAMS_acceptance_establishes_binding_without_rewriting_host_model', () => {
  binding.bindChild('ses_chat_params_root_2', 'ses_chat_params_child_2', 'deep-coder')
  binding.acceptPromptExecution(
    'ses_chat_params_child_2',
    'pk-chat-params',
    'physical-chat-params',
    'deep-coder',
    { providerID: 'anthropic', modelID: 'deep-opus' },
  )
  const output = { model: { providerID: 'anthropic', modelID: 'deep-opus' } }
  const observed = chatParams.apply(
    { sessionID: 'ses_chat_params_child_2', agent: 'deep-coder', model: { providerID: 'anthropic', modelID: 'deep-opus' } },
    output,
  )
  assert.equal(observed.ok, true, observed.error)
  assert.equal(observed.modelID, 'deep-opus')
  assert.equal(observed.temperature, 1)
  assert.equal(output.model.modelID, 'deep-opus')
})

test('WHAT[INTERACTION-AUTHORITY-011] CHAT_PARAMS_uses_the_resolved_provider_model_id_not_the_mutated_user_message_model', () => {
  binding.bindChild('ses_chat_params_root_3', 'ses_chat_params_child_3', 'deep-coder')
  binding.acceptPromptExecution(
    'ses_chat_params_child_3',
    'pk-chat-params-actual-model',
    'physical-chat-params-actual-model',
    'deep-coder',
    { providerID: 'anthropic', modelID: 'deep-opus', variant: 'high' },
  )

  const output = {}
  const observed = chatParams.apply(
    {
      sessionID: 'ses_chat_params_child_3',
      agent: 'deep-coder',
      model: { id: 'fast-haiku', providerID: 'anthropic' },
      message: {
        model: { providerID: 'anthropic', modelID: 'deep-opus', variant: 'high' },
      },
    },
    output,
  )

  assert.equal(observed.ok, false)
  assert.match(observed.error, /model\/reasoning drift/i)
})

test('WHAT[INTERACTION-AUTHORITY-011] CHAT_PARAMS_accepts_the_real_provider_model_shape_with_message_variant', () => {
  binding.bindChild('ses_chat_params_root_4', 'ses_chat_params_child_4', 'deep-coder')
  binding.acceptPromptExecution(
    'ses_chat_params_child_4',
    'pk-chat-params-real-shape',
    'physical-chat-params-real-shape',
    'deep-coder',
    { providerID: 'anthropic', modelID: 'deep-opus', variant: 'high' },
  )

  const inputModel = {
    id: 'deep-opus',
    providerID: 'anthropic',
    capabilities: { temperature: true },
    variants: { high: { reasoning: { effort: 'high' } }, low: {} },
    options: {},
  }
  const output = { options: { existing: 'sentinel' } }

  const observed = chatParams.apply(
    {
      sessionID: 'ses_chat_params_child_4',
      agent: 'deep-coder',
      model: inputModel,
      message: {
        model: { providerID: 'anthropic', modelID: 'deep-opus', variant: 'high' },
      },
    },
    output,
  )

  assert.equal(observed.ok, true, observed.error)
  assert.equal(observed.temperature, 1)
  assert.equal(output.temperature, 1)
  assert.equal(output.options.temperature, 1)
  assert.equal(output.options.existing, 'sentinel')
  assert.equal(inputModel.variants.high.temperature, 1)
  assert.equal(inputModel.variants.low.temperature, 1)
  assert.equal(inputModel.options.temperature, 1)
})

test('WHAT[INTERACTION-AUTHORITY-011] CHAT_PARAMS_leaves_temperature_untouched_when_model_capability_disables_it', () => {
  binding.bindChild('ses_chat_params_root_5', 'ses_chat_params_child_5', 'deep-coder')
  binding.acceptPromptExecution(
    'ses_chat_params_child_5',
    'pk-chat-params-reasoning-shape',
    'physical-chat-params-reasoning-shape',
    'deep-coder',
    { providerID: 'openai', modelID: 'o3-mini', variant: 'high' },
  )

  const inputModel = {
    id: 'o3-mini',
    providerID: 'openai',
    capabilities: { temperature: false },
    variants: { high: {} },
  }
  const output = { options: {} }
  const observed = chatParams.apply(
    {
      sessionID: 'ses_chat_params_child_5',
      agent: 'deep-coder',
      model: inputModel,
      message: {
        model: { providerID: 'openai', modelID: 'o3-mini', variant: 'high' },
      },
    },
    output,
  )

  assert.equal(observed.ok, true, observed.error)
  assert.equal(observed.temperature, undefined)
  assert.equal(output.temperature, undefined)
  assert.equal(output.options.temperature, undefined)
  assert.equal(inputModel.variants.high.temperature, undefined)
})

test('WHAT[INTERACTION-AUTHORITY-011] CHAT_PARAMS_agentless_root_does_not_invent_binding', () => {
  const output = { model: { providerID: 'anthropic', modelID: 'fast-haiku' } }
  const observed = chatParams.apply({ sessionID: 'ses_unbound_root' }, output)
  assert.equal(observed.ok, true)
  assert.equal(observed.temperature, undefined)
  assert.equal(output.model.modelID, 'fast-haiku')
})
