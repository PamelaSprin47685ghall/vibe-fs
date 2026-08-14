// Split from tests/unit/host/chat-params-hook.test.mjs (cutover Wave 2a);
// owner: host-boundary. chat.params 观察适配半边：观察 adapter 永不重写 Host
// output、不发明 binding（root 显式用户 model 不被覆盖、unknown agent / 空
// inventory / bare binding 均为 no-op）。
// parented/agent-less 的 binding 语义断言归 interaction-authority。

import assert from 'node:assert/strict'
import { fileURLToPath } from 'node:url'
import { dirname, join } from 'node:path'
import test from 'node:test'

const here = dirname(fileURLToPath(import.meta.url))
const { mapOf, resultOf } = await import('../../verification-system/tests/support/domain.mjs')

const { create } = await import(join(here, '../../../dist/Infrastructure/OpenCode/Host/ChatParamsHook.js'))
const { validate } = await import(join(here, '../../../dist/Infrastructure/OpenCode/Host/ManagedAgentConfig.js'))

const NAMES = [
  'fast-orchestrator', 'deep-orchestrator',
  'fast-manager', 'deep-manager',
  'fast-coder', 'deep-coder',
  'fast-inspector', 'deep-inspector',
  'fast-devops', 'deep-devops',
  'fast-browser', 'deep-browser',
  'fast-inquiry', 'deep-inquiry',
  'fast-reviewer', 'deep-reviewer',
  'fast-blogger', 'deep-blogger',
  'fast-distiller', 'deep-distiller',
  'fast-bookkeeper', 'deep-bookkeeper',
]

const slashConfig = () => {
  const agent = {}
  for (const name of NAMES) {
    agent[name] = { model: name.includes('fast') ? 'anthropic/fast-haiku' : 'anthropic/deep-opus' }
  }
  return { agent }
}

const bareConfig = () => {
  const agent = {}
  for (const name of NAMES) {
    agent[name] = { model: name.includes('fast') ? 'fast-model' : 'deep-model' }
  }
  return { agent }
}

const inventoryOf = (config) => {
  const parsed = resultOf(validate(config))
  assert.equal(parsed.ok, true, parsed.ok ? '' : parsed.error)
  return parsed.value
}

const applyHook = (hook, input, output) => {
  const next = hook(input, output)
  if (typeof next === 'function') next(output)
}

test('CHAT_PARAMS_root_session_does_not_override_explicit_user_model', () => {
  const hook = create(undefined, () => inventoryOf(slashConfig()))
  const output = { model: { providerID: 'anthropic', modelID: 'fast-haiku' } }
  applyHook(hook, { sessionID: 'ses_deep', agent: 'deep-coder' }, output)
  assert.equal(output.model.providerID, 'anthropic')
  assert.equal(output.model.modelID, 'fast-haiku')
})

test('CHAT_PARAMS_unknown_agent_does_not_invent_fast', () => {
  const hook = create(undefined, () => inventoryOf(slashConfig()))
  const output = { model: { providerID: 'anthropic', modelID: 'already-there' } }
  applyHook(hook, { sessionID: 'ses_unknown', agent: 'build' }, output)
  assert.equal(output.model.modelID, 'already-there')
})

test('CHAT_PARAMS_empty_inventory_is_a_noop', () => {
  const hook = create(undefined, () => ({ Bindings: mapOf({}) }))
  const output = { model: { providerID: 'anthropic', modelID: 'fast-haiku' } }
  applyHook(hook, { sessionID: 'ses_empty', agent: 'deep-coder' }, output)
  assert.equal(output.model.modelID, 'fast-haiku')
})

test('CHAT_PARAMS_root_bare_binding_is_a_noop', () => {
  const hook = create(undefined, () => inventoryOf(bareConfig()))
  const output = {}
  applyHook(hook, { sessionID: 'ses_bare', agent: 'deep-coder' }, output)
  assert.equal(output.model, undefined)
})
