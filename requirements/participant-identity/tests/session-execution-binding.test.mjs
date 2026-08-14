// Split from tests/unit/host/session-execution-binding.test.mjs (cutover Wave 2a);
// owner: participant-identity. PID-008（PROMPT-006 binding 解析律）：只有外部
// 用户选择重绑 root session —— 无 user binding 证明的内部 prompt fail-closed、
// chat.params 观察不持久化临时 override、用户切换后跟随新 binding。
// parented 发送边界拒绝漂移断言归 host-boundary（HOST-BOUNDARY-008）。

import assert from 'node:assert/strict'
import test from 'node:test'

const sessionsModule = await import('../../../dist/OpenCode/Host/Sessions.js')
const createPort = Object.entries(sessionsModule).find(([k]) => k.startsWith('InjectedSessionPort_$ctor'))?.[1]
import { create as createChatParams } from '../../../dist/OpenCode/Host/ChatParamsHook.js'
import { validate, configureFromHostConfig } from '../../../dist/OpenCode/Host/ManagedAgentConfig.js'
import { SessionIdModule_create as sessionId } from '../../../dist/Foundation/Identity.js'
import { runtimeResources } from '../../verification-system/tests/support/domain.mjs'

runtimeResources.installFromPackage()

const eventPort = { SubscribeTerminalListener: () => ({ Dispose: () => {} }) }
const preserve = { tag: 0, fields: [] }
const override = { tag: 1, fields: [] }

const inventory = () => {
  const agent = {}
  for (const role of ['orchestrator', 'manager', 'coder', 'inspector', 'devops', 'browser', 'inquiry', 'reviewer', 'blogger', 'distiller', 'bookkeeper']) {
    agent[`fast-${role}`] = { model: 'opencode-go/deepseek-v4-flash' }
    agent[`deep-${role}`] = { model: 'opencode-go/deepseek-v4' }
  }

  const parsed = validate({ agent })
  assert.equal(parsed.tag, 0)
  return parsed.fields[0]
}

const applyHook = (hook, input, output = {}) => {
  const next = hook(input, output)
  if (typeof next === 'function') next(output)
  return output
}

test('PROMPT_006_only_external_user_choice_rebinds_root_session', async () => {
  const root = sessionId('ses_binding_root')
  const sends = []
  let hook

  const port = createPort(
    {
      SendPrompt: async (sid, text, options) => {
        sends.push({ sid: sid.fields[0], text, options })
        applyHook(hook, {
          sessionID: sid.fields[0],
          agent: options.Agent,
          model: options.Model,
          message: { agent: options.Agent, model: options.Model },
        })
        return { tag: 0, fields: [{ fields: [`accepted-${sid.fields[0]}`] }] }
      },
    },
    eventPort,
  )

  hook = createChatParams(undefined, inventory)
  const subscription = port.SubscribeTerminal(root, () => {})

  try {
    const unproven = await port.SendPrompt(root, 'plugin has no user binding proof', {
      Model: { providerID: 'opencode-go', modelID: 'deepseek-v4' },
      Agent: 'deep-coder', Directory: undefined, Metadata: undefined, Tools: undefined, BindingIntent: preserve,
    })
    assert.equal(unproven.tag, 4, 'internal prompt must fail closed until a real user binding is observed')
    assert.equal(sends.length, 0)

    applyHook(hook, {
      sessionID: 'ses_binding_root',
      agent: 'title',
      model: { providerID: 'opencode-go', modelID: 'small-title-model' },
      message: { agent: 'deep-coder', model: { providerID: 'opencode-go', modelID: 'deepseek-v4' } },
    })

    const accidental = await port.SendPrompt(root, 'plugin mistake', {
      Model: { providerID: 'opencode-go', modelID: 'deepseek-v4-flash' },
      Agent: 'fast-coder', Directory: undefined, Metadata: undefined, Tools: undefined, BindingIntent: preserve,
    })
    assert.equal(accidental.tag, 4)
    assert.equal(sends.length, 0)

    const temporary = await port.SendPrompt(root, 'typed override', {
      Model: { providerID: 'opencode-go', modelID: 'deepseek-v4-flash' },
      Agent: 'fast-coder', Directory: undefined, Metadata: undefined, Tools: undefined, BindingIntent: override,
    })
    assert.equal(temporary.tag, 0)

    const stillDeep = await port.SendPrompt(root, 'ordinary continuation', {
      Model: { providerID: 'opencode-go', modelID: 'deepseek-v4' },
      Agent: 'deep-coder', Directory: undefined, Metadata: undefined, Tools: undefined, BindingIntent: preserve,
    })
    assert.equal(stillDeep.tag, 0, 'internal chat.params observation must not persist the temporary override')

    applyHook(hook, {
      sessionID: 'ses_binding_root',
      agent: 'title',
      model: { providerID: 'anthropic', modelID: 'small-title-model' },
      message: { agent: 'fast-coder', model: { providerID: 'opencode-go', modelID: 'deepseek-v4-flash' } },
    })

    const afterUserSwitch = await port.SendPrompt(root, 'follow external user choice', {
      Model: { providerID: 'opencode-go', modelID: 'deepseek-v4-flash' },
      Agent: 'fast-coder', Directory: undefined, Metadata: undefined, Tools: undefined, BindingIntent: preserve,
    })
    assert.equal(afterUserSwitch.tag, 0)
    assert.equal(sends.length, 3)
  } finally {
    subscription.Dispose()
  }
})

test('PROMPT_006_subsession_accepts_model_with_default_or_unconstrained_variant', async () => {
  const hook = createChatParams(undefined, inventory)
  const agent = {}
  for (const role of ['orchestrator', 'manager', 'coder', 'inspector', 'devops', 'browser', 'inquiry', 'reviewer', 'blogger', 'distiller', 'bookkeeper']) {
    agent[`fast-${role}`] = { model: 'opencode-go/deepseek-v4-flash' }
    agent[`deep-${role}`] = { model: 'opencode-go/deepseek-v4' }
  }
  configureFromHostConfig({ agent })

  const child = sessionId('ses_variant_child')
  const port = createPort(
    {
      CreateChildSession: async () => ({ tag: 0, fields: [child] }),
      SendPrompt: async () => ({ tag: 0, fields: [{ fields: ['accepted'] }] }),
    },
    eventPort,
  )

  const created = await port.CreateChildSession(sessionId('ses_variant_parent'), { Agent: 'fast-distiller' })
  assert.equal(created.tag, 0)
  const subscription = port.SubscribeTerminal(child, () => {})

  try {
    const sent = await port.SendPrompt(child, 'first opening', {
      Model: undefined,
      Agent: 'fast-distiller', Directory: undefined, Metadata: undefined, Tools: undefined, BindingIntent: preserve,
    })
    assert.equal(sent.tag, 0)

    // OpenCode runtime passes variant: 'default' or variant: undefined or camelCase providerId/modelId
    assert.doesNotThrow(() => {
      applyHook(hook, {
        sessionID: 'ses_variant_child',
        agent: 'fast-distiller',
        message: {
          agent: 'fast-distiller',
          model: { providerID: 'opencode-go', modelID: 'deepseek-v4-flash', variant: 'default' },
        },
      })
    })

    // Also accepts camelCase properties
    assert.doesNotThrow(() => {
      applyHook(hook, {
        sessionID: 'ses_variant_child',
        agent: 'fast-distiller',
        message: {
          agent: 'fast-distiller',
          model: { providerId: 'opencode-go', modelId: 'deepseek-v4-flash' },
        },
      })
    })
  } finally {
    subscription.Dispose()
  }
})

test('PROMPT_006_subsession_accepts_authorized_peer_fallback_override', async () => {
  const hook = createChatParams(undefined, inventory)
  const agent = {}
  for (const role of ['orchestrator', 'manager', 'coder', 'inspector', 'devops', 'browser', 'inquiry', 'reviewer', 'blogger', 'distiller', 'bookkeeper']) {
    agent[`fast-${role}`] = { model: 'opencode-go/deepseek-v4-flash' }
    agent[`deep-${role}`] = { model: 'opencode-go/deepseek-v4' }
  }
  configureFromHostConfig({ agent })

  const child = sessionId('ses_fallback_child')
  const port = createPort(
    {
      CreateChildSession: async () => ({ tag: 0, fields: [child] }),
      SendPrompt: async () => ({ tag: 0, fields: [{ fields: ['accepted'] }] }),
    },
    eventPort,
  )

  const created = await port.CreateChildSession(sessionId('ses_fallback_parent'), { Agent: 'fast-distiller' })
  assert.equal(created.tag, 0)
  const subscription = port.SubscribeTerminal(child, () => {})

  try {
    const opening = await port.SendPrompt(child, 'first opening', {
      Model: undefined,
      Agent: 'fast-distiller', Directory: undefined, Metadata: undefined, Tools: undefined, BindingIntent: preserve,
    })
    assert.equal(opening.tag, 0)

    // Fallback peer override: fast-distiller -> deep-distiller (with its bound model)
    assert.doesNotThrow(() => {
      applyHook(hook, {
        sessionID: 'ses_fallback_child',
        agent: 'deep-distiller',
        message: {
          agent: 'deep-distiller',
          model: { providerID: 'opencode-go', modelID: 'deepseek-v4' },
        },
      })
    })

    // Foreign agent drift is still rejected fail-closed
    assert.throws(() => {
      applyHook(hook, {
        sessionID: 'ses_fallback_child',
        agent: 'fast-coder',
        message: {
          agent: 'fast-coder',
          model: { providerID: 'opencode-go', modelID: 'deepseek-v4-flash' },
        },
      })
    }, /provider agent drift \(fast-distiller -> fast-coder\)/)
  } finally {
    subscription.Dispose()
  }
})
