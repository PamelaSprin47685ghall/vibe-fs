// host-boundary: parented sends preserve frozen EffectiveAgent at the physical
// Host boundary. Caller-supplied model is non-authoritative and is replaced by the
// execution-model-routing lease before the underlying Host port sees it.

import assert from 'node:assert/strict'
import { after } from 'node:test'
import test from 'node:test'
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { physicalUser } from '../../verification-system/tests/support/domain.mjs'

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

test('WHAT[HOST-BOUNDARY-008] PROMPT_006_production_wires_prompt_acceptance_to_the_exact_provider_attempt', () => {
  const hostSignal = readFileSync(new URL('../../../src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs', import.meta.url), 'utf8')
  const transforms = readFileSync(new URL('../../../src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs', import.meta.url), 'utf8')
  const sessions = readFileSync(new URL('../../../src/Wanxiangshu/OpenCode/Host/Sessions.fs', import.meta.url), 'utf8')
  const bindingSource = readFileSync(new URL('../../../src/Wanxiangshu/OpenCode/Host/SessionExecutionBinding.fs', import.meta.url), 'utf8')

  assert.match(hostSignal, /SessionExecutionBinding\.acceptPromptExecution/)
  assert.match(transforms, /SessionExecutionBinding\.beginProviderAttempt/)
  assert.doesNotMatch(sessions, /beginInternalSend|endInternalSend/)
  assert.doesNotMatch(bindingSource, /internalBindings|beginInternalSend|endInternalSend/)
})

test('WHAT[HOST-BOUNDARY-008] PROMPT_006_provider_attempt_keeps_typed_effective_agent_after_SendPrompt_stack_returns', async () => {
  const sid = sessionId('ses_binding_async_override')
  const promptKey = { fields: ['prompt-deep-continuation'] }
  const physical = physicalUser('msg-deep-continuation')
  const deepModel = { providerID: 'provider', modelID: 'deep-coder-leased', variant: 'none' }

  binding.observeUserFacingAgent(sid, 'fast-coder')
  await routing.ModelRouting_acquireManagedExecution(sid, physical, 'deep-coder')

  // Physical chat.message acceptance hands the typed continuation binding to the
  // provider-attempt boundary. The SendPrompt call stack is already gone when
  // chat.params eventually validates the provider request.
  binding.acceptPromptExecution(sid, promptKey, physical, 'deep-coder', deepModel)
  const began = binding.beginProviderAttempt(sid, physical, promptKey)
  assert.equal(began.tag, 0, 'accepted PromptKey must bind the concrete provider attempt')

  const allowed = binding.validateObservedProvider(sid, 'deep-coder', deepModel)
  assert.equal(allowed.tag, 0, allowed.tag === 0 ? '' : allowed.fields?.[0])
  assert.equal(allowed.fields[0], true)

  // One physical user prompt can drive several provider attempts across tool
  // calls. The exact same PromptKey remains execution authority for that turn.
  const resumed = binding.beginProviderAttempt(sid, physical, promptKey)
  assert.equal(resumed.tag, 0, resumed.tag === 0 ? '' : resumed.fields?.[0])
  const resumedAllowed = binding.validateObservedProvider(sid, 'deep-coder', deepModel)
  assert.equal(resumedAllowed.tag, 0, resumedAllowed.tag === 0 ? '' : resumedAllowed.fields?.[0])
  assert.equal(resumedAllowed.fields[0], true)

  // The override is not session authority. A later physical user message is a
  // new execution identity; chat.message routes it from the root/base agent and
  // atomically supersedes the deep continuation lease even without idle.
  const nextPhysical = physicalUser('msg-next-root')
  const fastModel = { providerID: 'provider', modelID: 'fast-coder-leased', variant: 'none' }
  await routing.ModelRouting_acquireManagedExecution(sid, nextPhysical, 'fast-coder')
  binding.acceptExternalExecution(sid, nextPhysical, 'fast-coder', fastModel)
  const next = binding.beginProviderAttempt(sid, nextPhysical, undefined)
  assert.equal(next.tag, 0)
  const stale = binding.validateObservedProvider(sid, 'deep-coder', deepModel)
  assert.equal(stale.tag, 1)
  assert.match(stale.fields[0], /provider agent drift \(fast-coder -> deep-coder\)/)

  binding.drop(sid)
})

test('WHAT[HOST-BOUNDARY-008] PROMPT_006_parented_send_is_model_free_but_rejects_agent_drift_before_host', async () => {
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
    assert.equal(sends[0].options.Agent, 'deep-coder')
    assert.equal(sends[0].options.Model, undefined, 'enqueue must not acquire or project a provider model')

    const stillLeased = await port.SendPrompt(
      child,
      'second',
      sendOptions('deep-coder', { providerID: 'another-placeholder', modelID: 'also-wrong' }),
    )
    assert.equal(stillLeased.tag, 0)
    assert.equal(sends[1].options.Agent, 'deep-coder')
    assert.equal(sends[1].options.Model, undefined)

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
