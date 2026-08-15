// PID-008 / PROMPT-006: external user messages own EffectiveAgent selection;
// execution model is always leased from execution-model-routing and never from Host config.

import assert from 'node:assert/strict'
import { mkdtemp, mkdir, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test, { after } from 'node:test'

const originalHome = process.env.HOME
const home = await mkdtemp(join(tmpdir(), 'wanxiangshu-binding-home-'))
process.env.HOME = home
await mkdir(join(home, '.config', 'opencode'), { recursive: true })
await writeFile(join(home, '.config', 'opencode', 'wanxiangshu.mjs'), `
export default function route(role) {
  if (role.startsWith('deep-')) return { model: 'test/deep', reasoning: 'high' }
  if (role.startsWith('fast-')) return { model: 'test/fast', reasoning: 'none' }
  return { model: 'test/system', reasoning: 'none' }
}
`, 'utf8')

const routing = await import('../../../dist/OpenCode/Host/ModelRouting.js')
await routing.ModelRouting_initialize()
const binding = await import('../../../dist/OpenCode/Host/SessionExecutionBinding.js')
const sessionsModule = await import('../../../dist/OpenCode/Host/Sessions.js')
const { create: createChatParams } = await import('../../../dist/OpenCode/Host/ChatParamsHook.js')
const { SessionIdModule_create: sessionId } = await import('../../../dist/Foundation/Identity.js')

const createPort = Object.entries(sessionsModule).find(([k]) => k.startsWith('InjectedSessionPort_$ctor'))?.[1]
const eventPort = { SubscribeTerminalListener: () => ({ Dispose: () => {} }) }
const preserve = { tag: 0, fields: [] }
const override = { tag: 1, fields: [] }

const options = (agent, intent = preserve) => ({
  Model: undefined,
  Agent: agent,
  Directory: undefined,
  Metadata: undefined,
  Tools: undefined,
  BindingIntent: intent,
})

const modelKey = (model) => `${model.providerID}/${model.modelID}[${model.variant}]`

const applyParams = (hook, sessionID, agent, model) => {
  const next = hook({
    sessionID,
    agent,
    model: { providerID: model.providerID, modelID: model.modelID },
    message: { agent, model },
  })
  if (typeof next === 'function') next({})
}

after(async () => {
  if (originalHome === undefined) delete process.env.HOME
  else process.env.HOME = originalHome
  await rm(home, { recursive: true, force: true })
})

test('PROMPT_006_root_requires_external_agent_proof_then_model_is_scheduler_owned', async () => {
  const root = sessionId('ses_binding_root')
  const sends = []
  const hook = createChatParams()

  const port = createPort({
    SendPrompt: async (sid, text, sent) => {
      sends.push({ sid: sid.fields[0], text, sent })
      applyParams(hook, sid.fields[0], sent.Agent, sent.Model)
      return { tag: 0, fields: [{ fields: [`accepted-${sid.fields[0]}`] }] }
    },
  }, eventPort)
  const subscription = port.SubscribeTerminal(root, () => {})

  try {
    const unproven = await port.SendPrompt(root, 'no external user binding', options('deep-coder'))
    assert.equal(unproven.tag, 4)
    assert.equal(sends.length, 0)

    binding.observeUserFacingAgent(root, 'deep-coder')
    const deep = await port.SendPrompt(root, 'ordinary continuation', options('deep-coder'))
    assert.equal(deep.tag, 0)
    assert.equal(modelKey(sends.at(-1).sent.Model), 'test/deep[high]')

    const temporary = await port.SendPrompt(root, 'peer fallback', options('fast-coder', override))
    assert.equal(temporary.tag, 0)
    assert.equal(modelKey(sends.at(-1).sent.Model), 'test/fast[none]')

    const stillDeep = await port.SendPrompt(root, 'ordinary continuation', options('deep-coder'))
    assert.equal(stillDeep.tag, 0, 'internal peer override cannot rewrite external user selection')
    assert.equal(modelKey(sends.at(-1).sent.Model), 'test/deep[high]')

    binding.observeUserFacingAgent(root, 'fast-coder')
    const switched = await port.SendPrompt(root, 'follow next external user choice', options('fast-coder'))
    assert.equal(switched.tag, 0)
    assert.equal(modelKey(sends.at(-1).sent.Model), 'test/fast[none]')
  } finally {
    binding.drop(root)
    subscription.Dispose()
  }
})

test('PROMPT_006_parented_session_uses_stable_agent_lease_and_authorized_peer_only', async () => {
  const parent = sessionId('ses_parent')
  const child = sessionId('ses_child')
  const sends = []
  const hook = createChatParams()

  const port = createPort({
    CreateChildSession: async () => ({ tag: 0, fields: [child] }),
    SendPrompt: async (sid, text, sent) => {
      sends.push({ sid: sid.fields[0], text, sent })
      applyParams(hook, sid.fields[0], sent.Agent, sent.Model)
      return { tag: 0, fields: [{ fields: ['accepted'] }] }
    },
  }, eventPort)

  const created = await port.CreateChildSession(parent, { Agent: 'fast-distiller' })
  assert.equal(created.tag, 0)
  const subscription = port.SubscribeTerminal(child, () => {})

  try {
    const opening = await port.SendPrompt(child, 'opening', options('fast-distiller'))
    assert.equal(opening.tag, 0)
    assert.equal(modelKey(sends.at(-1).sent.Model), 'test/fast[none]')

    const peer = await port.SendPrompt(child, 'fallback', options('deep-distiller', override))
    assert.equal(peer.tag, 0)
    assert.equal(modelKey(sends.at(-1).sent.Model), 'test/deep[high]')

    const foreign = await port.SendPrompt(child, 'illegal role change', options('fast-coder', override))
    assert.equal(foreign.tag, 4)
    assert.match(foreign.fields[0], /not the peer|override/i)
  } finally {
    binding.drop(child)
    subscription.Dispose()
  }
})

test('PROMPT_006_provider_reasoning_variant_must_match_the_exact_lease', async () => {
  const child = sessionId('ses_variant_exact')
  binding.bind(sessionId('ses_variant_parent'), child, 'deep-distiller')
  await routing.ModelRouting_acquireManaged(child, 'deep-distiller')
  const hook = createChatParams()

  assert.doesNotThrow(() => applyParams(hook, 'ses_variant_exact', 'deep-distiller', {
    providerID: 'test', modelID: 'deep', variant: 'high',
  }))

  assert.throws(() => applyParams(hook, 'ses_variant_exact', 'deep-distiller', {
    providerID: 'test', modelID: 'deep', variant: 'default',
  }), /model\/reasoning drift/i)

  binding.drop(child)
})
