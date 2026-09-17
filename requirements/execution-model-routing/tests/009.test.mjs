import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const routing = await import("../../../dist/OpenCode/Host/ModelRoutingSurface.js");

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
    agent: 'engineer',
    model: { providerID: 'provider', modelID: 'model', variant: 'high' },
  }))

  assert.deepEqual(payload.body.model, { providerID: 'provider', modelID: 'model' })
  assert.equal(payload.body.variant, 'high')
  assert.equal(payload.variant, 'high')
  assert.equal('variant' in payload.body.model, false)
  assert.deepEqual(payload.model, { providerID: 'provider', modelID: 'model' })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { default: plugin } = await import("../../../dist/OpenCode/Plugin/Plugin.js");
const { createEnvironment, managedConfig } = await import("./support/process-shared-routing.mjs");


test('WHAT[EMR-009] EMR_009_chat_message_routes_when_session_id_is_carried_on_output_message', async () => {
  const environment = createEnvironment(plugin.server)
  const previousHome = process.env.HOME
  process.env.HOME = environment.home
  let hooks

  try {
    hooks = await environment.createPlugin('output-session-workspace')
    await hooks.config(managedConfig())

    const output = {
      message: {
        id: 'msg-output-only',
        role: 'user',
        sessionID: 'ses-output-only',
        agent: 'engineer',
        model: { providerID: 'host', modelID: 'placeholder' },
      },
      parts: [],
    }

    await hooks['chat.message']({ messageID: 'msg-output-only' }, output)
    assert.deepEqual(
      [output.message.model.providerID, output.message.model.modelID, output.message.model.variant],
      ['provider', 'model-a', 'none'],
      'chat.message must decode sessionID from output.message and route successfully',
    )
  } finally {
    if (hooks) await hooks.dispose()
    process.env.HOME = previousHome
    environment.dispose()
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { readFile } = await import("node:fs/promises");
const { default: test } = await import("node:test");

const source = async (relative) => readFile(new URL(`../../../${relative}`, import.meta.url), 'utf8')

test('WHAT[EMR-009] EMR_009_chat_message_is_the_single_managed_execution_admission_owner', async () => {
  const host = await source('src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs')
  const admission = await source('src/Wanxiangshu/OpenCode/Host/ChatAdmission/Transaction.fs')
  const routing = await source('src/Wanxiangshu/OpenCode/Host/ModelRouting.fs')
  const binding = await source('src/Wanxiangshu/OpenCode/Host/SessionExecutionBinding.fs')
  const sessions = await source('src/Wanxiangshu/OpenCode/Host/Sessions.fs')
  const params = await source('src/Wanxiangshu/OpenCode/Host/ChatParamsHook.fs')

  assert.match(host, /decoded\.PhysicalUserMessageId/)
  assert.match(host, /ChatAdmissionTransaction\.production/)
  assert.match(host, /ChatAdmissionTransaction\.execute/)
  assert.match(host, /ModelRouting\.projectHostModel output/)
  assert.match(admission, /Acquire =\s*fun witness ->[\s\S]*ModelRouting\.acquireExecutionAdmission/)
  assert.match(admission, /Bind = bind/)
  assert.match(admission, /Commit = ModelRouting\.commitExecutionAdmission/)
  assert.match(admission, /ReleaseBeforeProvider =\s*fun lease ->[\s\S]*ModelRouting\.releaseExecutionAdmissionBeforeProvider lease lease\.Identity/)
  assert.doesNotMatch(host, /ModelRoutingAcquisition|ChatExecutionAdmission\.(NoRoute|Rejected|ExternalManaged|PluginManaged)/)
  assert.doesNotMatch(host, /ModelRouting\.acquireExecutionAdmission|ModelRouting\.commitExecutionAdmission|ModelRouting\.releaseExecutionAdmissionBeforeProvider/)
  assert.doesNotMatch(host, /ModelRouting\.routeChatExecution|ModelRouting\.projectRoutedModel/)
  assert.doesNotMatch(host, /SessionExecutionBinding\.acceptRoutedExecution/)
  assert.doesNotMatch(host, /acceptPromptExecution|acceptExternalExecution/)
  assert.doesNotMatch(binding, /let acceptRoutedExecution/)
  assert.doesNotMatch(binding, /PromptDispatcher|DispatchAccepted/)
  assert.doesNotMatch(sessions, /ModelRouting\.acquireManagedExecution/, 'fork/send enqueue must never wait for model capacity')
  assert.doesNotMatch(host, /message\?model\s*<-/)
  assert.match(routing, /message\?model\s*<-\s*box model/)
  assert.match(params, /validateObservedProvider/)
  assert.doesNotMatch(params, /observeUserFacing\s/)
})
}
