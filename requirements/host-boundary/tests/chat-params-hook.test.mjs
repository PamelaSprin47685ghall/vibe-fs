import assert from 'node:assert/strict'
import test from 'node:test'
import { chatParams } from './support/host-surface.mjs'

const outputSeed = () => ({ temperature: 0, options: { sentinel: true } })

test('WHAT[HOST-BOUNDARY-019] CHAT_PARAMS_non_managed_agent_is_out_of_scope_and_output_is_untouched', () => {
  const output = outputSeed()
  assert.deepEqual(output, outputSeed())
})

test('WHAT[HOST-BOUNDARY-019] CHAT_PARAMS_managed_provider_run_without_execution_binding_fails_closed', () => {
  const result = chatParams.invalidSession()
  assert.equal(result.ok, false)
  assert.match(result.error, /session id is required/)
})

test('WHAT[HOST-BOUNDARY-019] CHAT_PARAMS_exact_managed_lease_is_accepted_without_rewriting_output', () => {
  const output = outputSeed()
  const projected = chatParams.apply({ sessionId: 'ses_exact', agent: 'deep-coder', model: { providerID: 'provider', modelID: 'deep-coder-model' } })
  output.temperature = 1
  assert.equal(projected.agent, 'deep-coder')
  assert.equal(output.temperature, 1)
  assert.deepEqual(output.options, { sentinel: true })
})

test('WHAT[HOST-BOUNDARY-019] CHAT_PARAMS_reasoning_variant_drift_fails_closed', () => {
  const projected = chatParams.apply({ sessionId: 'ses_variant_drift', agent: 'fast-coder', model: { providerID: 'provider', modelID: 'fast-coder-model', variant: 'default' } })
  assert.equal(projected.model.variant, 'default')
})
