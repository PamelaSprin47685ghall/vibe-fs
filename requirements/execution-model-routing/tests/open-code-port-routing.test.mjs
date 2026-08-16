import assert from 'node:assert/strict'
import test from 'node:test'

const {
  OpenCodePort_SdkClientPort: SdkClientPort,
} = await import('../../../dist/OpenCode/Host/OpenCodePort.js')
const { SessionIdModule_create: sessionId } = await import('../../../dist/Foundation/Identity.js')

const promptOptions = (overrides = {}) => ({
  Model: undefined,
  Agent: undefined,
  Directory: undefined,
  Metadata: undefined,
  Tools: undefined,
  BindingIntent: { tag: 0 },
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
  const port = new SdkClientPort(client, undefined)

  await port.SendPrompt(sessionId('session-1'), 'hello', promptOptions({
    Agent: 'deep-coder',
    Model: { providerID: 'provider', modelID: 'model', variant: 'high' },
  }))

  assert.deepEqual(payload.body.model, { providerID: 'provider', modelID: 'model' })
  assert.equal(payload.body.variant, 'high')
  assert.equal(payload.variant, 'high')
  assert.equal('variant' in payload.body.model, false)
  assert.deepEqual(payload.model, { providerID: 'provider', modelID: 'model' })
})

test('WHAT[EMR-004] EMR_004_sdk_prompt_async_enqueue_never_waits_for_the_host_run_promise', async () => {
  let releaseHost
  let invoked = false
  const hostRun = new Promise((resolve) => {
    releaseHost = resolve
  })
  const client = {
    session: {
      promptAsync: () => {
        invoked = true
        return hostRun
      },
    },
  }
  const port = new SdkClientPort(client, undefined)

  let settled = false
  const sending = port.SendPrompt(
    sessionId('session-detached'),
    'start child work',
    promptOptions({ Agent: 'deep-devops' }),
  ).then((value) => {
    settled = true
    return value
  })

  await new Promise((resolve) => setImmediate(resolve))
  assert.equal(invoked, true, 'the Host enqueue API is invoked')
  assert.equal(settled, true, 'SendPrompt must return at enqueue, not when the child run promise settles')

  await sending
  releaseHost({})
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
  const port = new SdkClientPort(client, undefined)

  await port.SendPrompt(sessionId('session-2'), 'hello', promptOptions({ Agent: 'fast-coder' }))

  assert.equal(payload.model, undefined)
  assert.equal(payload.variant, undefined)
  assert.equal(payload.body.model, undefined)
  assert.equal(payload.body.variant, undefined)
})
