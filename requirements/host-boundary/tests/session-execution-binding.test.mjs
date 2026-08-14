// Split from tests/unit/host/session-execution-binding.test.mjs (cutover Wave 2a);
// owner: host-boundary. HOST-BOUNDARY-008 / PROMPT-008 物理身份：InjectedSessionPort
// 发送边界拒绝漂移 —— parented session 在 host send 前拒绝 agent/model drift。
// root 重绑解析律断言归 participant-identity（PID-008）。

import assert from 'node:assert/strict'
import test from 'node:test'

const sessionsModule = await import('../../../dist/OpenCode/Host/Sessions.js')
const createPort = Object.entries(sessionsModule).find(([k]) => k.startsWith('InjectedSessionPort_$ctor'))?.[1]
import { SessionIdModule_create as sessionId } from '../../../dist/Foundation/Identity.js'

const eventPort = { SubscribeTerminalListener: () => ({ Dispose: () => {} }) }
const preserve = { tag: 0, fields: [] }

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
