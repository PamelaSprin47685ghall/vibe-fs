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
  assert.match(rejected.error, /no observable provider\/model binding/)
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

test('WHAT[INTERACTION-AUTHORITY-011] CHAT_PARAMS_agentless_root_does_not_invent_binding', () => {
  const output = { model: { providerID: 'anthropic', modelID: 'fast-haiku' } }
  const observed = chatParams.apply({ sessionID: 'ses_unbound_root' }, output)
  assert.equal(observed.ok, true)
  assert.equal(observed.temperature, undefined)
  assert.equal(output.model.modelID, 'fast-haiku')
})
