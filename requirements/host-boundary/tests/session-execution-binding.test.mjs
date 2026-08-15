// host-boundary: parented sends preserve frozen EffectiveAgent at the physical
// Host boundary. Caller-supplied model is non-authoritative and is replaced by the
// execution-model-routing lease before the underlying Host port sees it.

import assert from 'node:assert/strict'
import { after } from 'node:test'
import test from 'node:test'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

const previousHome = process.env.HOME
const previousUserProfile = process.env.USERPROFILE
const home = mkdtempSync(join(tmpdir(), 'wxs-host-binding-routing-'))
const routingDir = join(home, '.config', 'opencode')
mkdirSync(routingDir, { recursive: true })
writeFileSync(
  join(routingDir, 'wanxiangshu.mjs'),
  `export default function route(role) {
  return { model: 'provider/' + role + '-leased', reasoning: 'none' }
}\n`,
  'utf8',
)
process.env.HOME = home
process.env.USERPROFILE = home

const routing = await import('../../../dist/OpenCode/Host/ModelRouting.js')
await routing.ModelRouting_initialize()
const sessionsModule = await import('../../../dist/OpenCode/Host/Sessions.js')
const binding = await import('../../../dist/OpenCode/Host/SessionExecutionBinding.js')
const createPort = Object.entries(sessionsModule).find(([k]) => k.startsWith('InjectedSessionPort_$ctor'))?.[1]
const { SessionIdModule_create: sessionId } = await import('../../../dist/Foundation/Identity.js')

const eventPort = { SubscribeTerminalListener: () => ({ Dispose: () => {} }) }
const preserve = { tag: 0, fields: [] }

const sendOptions = (agent, model) => ({
  Model: model,
  Agent: agent,
  Directory: undefined,
  Metadata: undefined,
  Tools: undefined,
  BindingIntent: preserve,
})

after(() => {
  if (previousHome === undefined) delete process.env.HOME
  else process.env.HOME = previousHome
  if (previousUserProfile === undefined) delete process.env.USERPROFILE
  else process.env.USERPROFILE = previousUserProfile
  rmSync(home, { recursive: true, force: true })
})

test('PROMPT_006_provider_attempt_keeps_typed_effective_agent_after_SendPrompt_stack_returns', async () => {
  const sid = sessionId('ses_binding_async_override')
  const promptKey = { fields: ['prompt-deep-continuation'] }
  const deepModel = { providerID: 'provider', modelID: 'deep-coder-leased', variant: 'none' }

  binding.observeUserFacingAgent(sid, 'fast-coder')

  // Physical chat.message acceptance hands the typed continuation binding to the
  // provider-attempt boundary. The SendPrompt call stack is already gone when
  // chat.params eventually validates the provider request.
  binding.acceptPromptExecution(sid, promptKey, 'deep-coder', deepModel)
  const began = binding.beginProviderAttempt(sid, promptKey)
  assert.equal(began.tag, 0, 'accepted PromptKey must bind the concrete provider attempt')

  const allowed = binding.validateObservedProvider(sid, 'deep-coder', deepModel)
  assert.equal(allowed.tag, 0, allowed.tag === 0 ? '' : allowed.fields?.[0])
  assert.equal(allowed.fields[0], true)

  // The override is not session authority. A later provider attempt with no
  // matching PromptKey falls back to the root/base binding and must reject deep.
  const next = binding.beginProviderAttempt(sid, undefined)
  assert.equal(next.tag, 0)
  const stale = binding.validateObservedProvider(sid, 'deep-coder', deepModel)
  assert.equal(stale.tag, 1)
  assert.match(stale.fields[0], /provider agent drift \(fast-coder -> deep-coder\)/)

  binding.drop(sid)
})

test('PROMPT_006_parented_send_overrides_model_but_rejects_agent_drift_before_host', async () => {
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
    const accepted = await port.SendPrompt(
      child,
      'first',
      sendOptions('deep-coder', { providerID: 'host-placeholder', modelID: 'wrong-model' }),
    )
    assert.equal(accepted.tag, 0)
    assert.equal(sends[0].options.Model.providerID, 'provider')
    assert.equal(sends[0].options.Model.modelID, 'deep-coder-leased')
    assert.equal(sends[0].options.Model.variant, 'none')

    const stillLeased = await port.SendPrompt(
      child,
      'second',
      sendOptions('deep-coder', { providerID: 'another-placeholder', modelID: 'also-wrong' }),
    )
    assert.equal(stillLeased.tag, 0)
    assert.equal(sends[1].options.Model.modelID, 'deep-coder-leased')

    const wrongAgent = await port.SendPrompt(
      child,
      'wrong agent',
      sendOptions('fast-coder', { providerID: 'provider', modelID: 'fast-coder-leased' }),
    )
    assert.equal(wrongAgent.tag, 4)
    assert.match(wrongAgent.fields[0], /agent drift/i)
    assert.equal(sends.length, 2)
  } finally {
    binding.drop(child)
    subscription.Dispose()
  }
})
