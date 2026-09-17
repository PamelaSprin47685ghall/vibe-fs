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

  await sendPrompt(port, 'session-2', 'hello', promptOptions({ agent: 'engineer' }))

  assert.equal(payload.model, undefined)
  assert.equal(payload.variant, undefined)
  assert.equal(payload.body.model, undefined)
  assert.equal(payload.body.variant, undefined)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { readFile } = await import("node:fs/promises");
const { default: test } = await import("node:test");

const source = async (relative) => readFile(new URL(`../../../${relative}`, import.meta.url), 'utf8')

test('WHAT[EMR-008] EMR_008_host_inventory_no_longer_exposes_model_binding_authority', async () => {
  const managed = await source('src/Wanxiangshu/OpenCode/Host/ManagedAgentConfig.fs')
  const port = await source('src/Wanxiangshu/OpenCode/Host/OpenCodePort.fs')
  const wiring = await source('src/Wanxiangshu/OpenCode/Plugin/PluginSessionWiring.fs')
  const strengthScope = await source('src/Wanxiangshu/Strength/OpenCode/PluginScope.fs')

  assert.doesNotMatch(managed, /tryBoundModel|tryOpencodeModel|DuplicatePairModel|liveInventory|Model:\s*string/)
  assert.doesNotMatch(port, /ManagedAgentConfig\.tryBoundModel/)
  assert.doesNotMatch(wiring, /ManagedAgentInventory|tryOpencodeModel|promptModelFor/)
  assert.doesNotMatch(strengthScope, /ManagedAgentInventory|RecordManagedAgentInventory/)
})
test('WHAT[EMR-008] SPEC_INV_fast_and_deep_physical_model_equality_is_not_an_eligibility_gate', async () => {
  const policy = await source('src/Wanxiangshu/Strength/Policy.fs')
  const speculate = await source('src/Wanxiangshu/Strength/OpenCode/Speculate.fs')

  assert.doesNotMatch(policy, /ModelBindingsDistinct|model-bindings-not-distinct/)
  assert.doesNotMatch(speculate, /ManagedAgentInventory|modelsDistinct|fastBinding/)
})
}
