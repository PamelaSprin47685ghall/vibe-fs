import assert from 'node:assert/strict'
import test from 'node:test'

import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'

const { createSdkClientPort, sendPrompt } = routing

const promptOptions = (overrides = {}) => ({
  model: undefined,
  agent: undefined,
  directory: undefined,
  metadata: undefined,
  tools: undefined,
  bindingIntent: 'Preserve',
  ...overrides,
})

test('WHAT[EMR-009] EMR_009_sdk_prompt_projects_model_without_nested_variant_and_reasoning_as_top_level_variant', async () => {
  let payload
  const client = {
    session: {
      promptAsync: async (value) => {
        payload = value
        return {}
      },
    },
  }
  const port = createSdkClientPort(client)

  await sendPrompt(port, 'session-1', 'hello', promptOptions({
    agent: 'coder',
    model: { providerID: 'provider', modelID: 'model', variant: 'high' },
  }))

  assert.deepEqual(payload.body.model, { providerID: 'provider', modelID: 'model' })
  assert.equal(payload.body.variant, 'high')
  assert.equal(payload.variant, 'high')
  assert.equal('variant' in payload.body.model, false)
  assert.deepEqual(payload.model, { providerID: 'provider', modelID: 'model' })
})

test('WHAT[EMR-004] EMR_004_sdk_prompt_async_awaits_host_enqueue_and_surfaces_enqueue_rejection', async () => {
  let releaseHost
  let rejectHost
  let invoked = false
  const hostRun = new Promise((resolve) => {
    releaseHost = resolve
  })
  const failingRun = new Promise((_, reject) => { rejectHost = reject })
  const client = {
    session: {
      promptAsync: () => {
        invoked = true
        return hostRun
      },
    },
  }
  const port = createSdkClientPort(client)


  const sending = sendPrompt(
    port,
    'session-detached',
    'start child work',
    promptOptions({ agent: 'devops' }),
  )
  releaseHost({})
  const result = await sending
  assert.equal(invoked, true, 'the Host enqueue API is invoked')
  assert.match(JSON.stringify(result), /AdmittedWithReceipt/i)

  const failingPort = createSdkClientPort({ session: { promptAsync: () => failingRun } })
  rejectHost(new Error('Host enqueue refused'))
  const failed = await sendPrompt(failingPort, 'session-failing', 'refused work', promptOptions({ agent: 'devops' }))
  assert.match(JSON.stringify(failed), /Fatal|refused/i)
})

test('WHAT[EMR-008] EMR_008_sdk_prompt_never_recovers_a_model_from_agent_or_host_inventory', async () => {
  let payload
  const client = {
    session: {
      promptAsync: async (value) => {
        payload = value
        return {}
      },
    },
  }
  const port = createSdkClientPort(client)

  await sendPrompt(port, 'session-2', 'hello', promptOptions({ agent: 'coder' }))

  assert.equal(payload.model, undefined)
  assert.equal(payload.variant, undefined)
  assert.equal(payload.body.model, undefined)
  assert.equal(payload.body.variant, undefined)
})
