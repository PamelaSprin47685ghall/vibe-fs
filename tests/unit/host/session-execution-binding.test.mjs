import assert from 'node:assert/strict'
import test from 'node:test'

import { InjectedSessionPort_$ctor_Z60D0357E as createPort } from '../../../dist/Infrastructure/OpenCode/Host/Sessions.js'
import { create as createChatParams } from '../../../dist/Infrastructure/OpenCode/Host/ChatParamsHook.js'
import { validate } from '../../../dist/Infrastructure/OpenCode/Host/ManagedAgentConfig.js'
import { SessionIdModule_create as sessionId } from '../../../dist/Kernel/Identity.js'

const eventPort = { SubscribeTerminalListener: () => ({ Dispose: () => {} }) }
const preserve = { tag: 0, fields: [] }
const override = { tag: 1, fields: [] }

const inventory = () => {
  const agent = {}
  for (const role of ['orchestrator', 'manager', 'coder', 'inspector', 'devops', 'browser', 'inquiry', 'reviewer', 'blogger', 'distiller', 'bookkeeper']) {
    agent[`fast-${role}`] = { model: 'anthropic/fast-haiku' }
    agent[`deep-${role}`] = { model: 'anthropic/deep-opus' }
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

test('PROMPT_006_parented_session_rejects_agent_and_model_drift_before_host_send', async () => {
  const child = sessionId('ses_binding_child')
  const sends = []
  const port = createPort(
    {
      CreateChildSession: async () => ({ tag: 0, fields: [child] }),
      SendPrompt: async (sid, text, options) => {
        sends.push({ sid: sid.fields[0], text, options })
        return { tag: 0, fields: [{ fields: [`accepted-${sid.fields[0]}`] }] }
      },
    },
    eventPort,
  )

  const created = await port.CreateChildSession(sessionId('ses_binding_parent'), { Agent: 'deep-coder' })
  assert.equal(created.tag, 0)
  const subscription = port.SubscribeTerminal(child, () => {})

  try {
    const accepted = await port.SendPrompt(child, 'first', {
      Model: { providerID: 'anthropic', modelID: 'deep-opus' },
      Agent: 'deep-coder', Directory: undefined, Metadata: undefined, Tools: undefined, BindingIntent: preserve,
    })
    assert.equal(accepted.tag, 0)

    const wrongModel = await port.SendPrompt(child, 'wrong model', {
      Model: { providerID: 'anthropic', modelID: 'fast-haiku' },
      Agent: 'deep-coder', Directory: undefined, Metadata: undefined, Tools: undefined, BindingIntent: preserve,
    })
    assert.equal(wrongModel.tag, 4)

    const wrongAgent = await port.SendPrompt(child, 'wrong agent', {
      Model: { providerID: 'anthropic', modelID: 'fast-haiku' },
      Agent: 'fast-coder', Directory: undefined, Metadata: undefined, Tools: undefined, BindingIntent: preserve,
    })
    assert.equal(wrongAgent.tag, 4)
    assert.equal(sends.length, 1)
  } finally {
    subscription.Dispose()
  }
})

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
      Model: { providerID: 'anthropic', modelID: 'deep-opus' },
      Agent: 'deep-coder', Directory: undefined, Metadata: undefined, Tools: undefined, BindingIntent: preserve,
    })
    assert.equal(unproven.tag, 4, 'internal prompt must fail closed until a real user binding is observed')
    assert.equal(sends.length, 0)

    applyHook(hook, {
      sessionID: 'ses_binding_root',
      agent: 'title',
      model: { providerID: 'anthropic', modelID: 'small-title-model' },
      message: { agent: 'deep-coder', model: { providerID: 'anthropic', modelID: 'deep-opus' } },
    })

    const accidental = await port.SendPrompt(root, 'plugin mistake', {
      Model: { providerID: 'anthropic', modelID: 'fast-haiku' },
      Agent: 'fast-coder', Directory: undefined, Metadata: undefined, Tools: undefined, BindingIntent: preserve,
    })
    assert.equal(accidental.tag, 4)
    assert.equal(sends.length, 0)

    const temporary = await port.SendPrompt(root, 'typed override', {
      Model: { providerID: 'anthropic', modelID: 'fast-haiku' },
      Agent: 'fast-coder', Directory: undefined, Metadata: undefined, Tools: undefined, BindingIntent: override,
    })
    assert.equal(temporary.tag, 0)

    const stillDeep = await port.SendPrompt(root, 'ordinary continuation', {
      Model: { providerID: 'anthropic', modelID: 'deep-opus' },
      Agent: 'deep-coder', Directory: undefined, Metadata: undefined, Tools: undefined, BindingIntent: preserve,
    })
    assert.equal(stillDeep.tag, 0, 'internal chat.params observation must not persist the temporary override')

    applyHook(hook, {
      sessionID: 'ses_binding_root',
      agent: 'title',
      model: { providerID: 'anthropic', modelID: 'small-title-model' },
      message: { agent: 'fast-coder', model: { providerID: 'anthropic', modelID: 'fast-haiku' } },
    })

    const afterUserSwitch = await port.SendPrompt(root, 'follow external user choice', {
      Model: { providerID: 'anthropic', modelID: 'fast-haiku' },
      Agent: 'fast-coder', Directory: undefined, Metadata: undefined, Tools: undefined, BindingIntent: preserve,
    })
    assert.equal(afterUserSwitch.tag, 0)
    assert.equal(sends.length, 3)
  } finally {
    subscription.Dispose()
  }
})
