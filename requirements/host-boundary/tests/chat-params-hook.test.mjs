// host-boundary: chat.params validates the physical provider binding and
// pins managed request temperature = 1.0.

import assert from 'node:assert/strict'
import { after } from 'node:test'
import test from 'node:test'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { physicalUser, sessionId } from '../../verification-system/tests/support/domain.mjs'

const previousHome = process.env.HOME
const previousUserProfile = process.env.USERPROFILE
const home = mkdtempSync(join(tmpdir(), 'wxs-chat-params-routing-'))
const routingDir = join(home, '.config', 'opencode')
mkdirSync(routingDir, { recursive: true })
writeFileSync(
  join(routingDir, 'wanxiangshu.mjs'),
  `export default function route(role) {
  return { model: 'provider/' + role + '-model', reasoning: 'none' }
}\n`,
  'utf8',
)
process.env.HOME = home
process.env.USERPROFILE = home

const routing = await import('../../../dist/OpenCode/Host/ModelRouting.js')
const binding = await import('../../../dist/OpenCode/Host/SessionExecutionBinding.js')
const { create } = await import('../../../dist/OpenCode/Host/ChatParamsHook.js')
await routing.ModelRouting_initialize()

const hook = create()
const applyHook = (input, output) => {
  const next = hook(input)
  if (typeof next === 'function') next(output)
}
const outputSeed = () => ({ temperature: 0, options: { sentinel: true } })

const managedInput = (sessionID, agent, variant = 'none') => ({
  sessionID,
  agent,
  model: { providerID: 'provider', modelID: `${agent}-model` },
  message: {
    agent,
    model: { providerID: 'provider', modelID: `${agent}-model`, variant },
  },
})

after(() => {
  if (previousHome === undefined) delete process.env.HOME
  else process.env.HOME = previousHome
  if (previousUserProfile === undefined) delete process.env.USERPROFILE
  else process.env.USERPROFILE = previousUserProfile
  rmSync(home, { recursive: true, force: true })
})

test('WHAT[HOST-BOUNDARY-019] CHAT_PARAMS_non_managed_agent_is_out_of_scope_and_output_is_untouched', () => {
  const output = outputSeed()
  applyHook({ sessionID: 'ses_unknown', agent: 'build', model: {} }, output)
  assert.deepEqual(output, outputSeed())
})

test('WHAT[HOST-BOUNDARY-019] CHAT_PARAMS_managed_provider_run_without_execution_binding_fails_closed', () => {
  assert.throws(
    () => applyHook(managedInput('ses_unbound', 'deep-coder'), outputSeed()),
    /not recognized as a bound session|no model-routing lease/i,
  )
})

test('WHAT[HOST-BOUNDARY-019] CHAT_PARAMS_exact_managed_lease_is_accepted_without_rewriting_output', async () => {
  const sid = sessionId('ses_exact')
  const physical = physicalUser('msg-exact')
  const model = { providerID: 'provider', modelID: 'deep-coder-model', variant: 'none' }
  binding.observeUserFacingAgent(sid, 'deep-coder')
  await routing.ModelRouting_acquireManagedExecution(sid, physical, 'deep-coder')
  binding.acceptExternalExecution(sid, physical, 'deep-coder', model)

  const output = outputSeed()
  assert.doesNotThrow(() => applyHook(managedInput('ses_exact', 'deep-coder'), output))
  assert.equal(output.temperature, 1)
  assert.deepEqual(output.options, { sentinel: true })
  binding.drop(sid)
})

test('WHAT[HOST-BOUNDARY-019] CHAT_PARAMS_reasoning_variant_drift_fails_closed', async () => {
  const sid = sessionId('ses_variant_drift')
  const physical = physicalUser('msg-variant-drift')
  const model = { providerID: 'provider', modelID: 'fast-coder-model', variant: 'none' }
  binding.observeUserFacingAgent(sid, 'fast-coder')
  await routing.ModelRouting_acquireManagedExecution(sid, physical, 'fast-coder')
  binding.acceptExternalExecution(sid, physical, 'fast-coder', model)

  assert.throws(
    () => applyHook(managedInput('ses_variant_drift', 'fast-coder', 'default'), outputSeed()),
    /model\/reasoning drift/i,
  )
  binding.drop(sid)
})
