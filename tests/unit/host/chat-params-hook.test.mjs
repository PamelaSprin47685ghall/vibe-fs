// ChatParamsHook: pin the request agent's bound model so Host cannot
// silently resolve an agent-less / history-inferred request to Fast.

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import { dirname } from 'node:path'

const here = dirname(fileURLToPath(import.meta.url))
const { agentFact, agentJournal, authorityRoot, logicalRunId, mapOf, resultOf, sessionId, stream } = await import(
  '../support/domain.mjs'
)

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

test('CHAT_PARAMS_deep_agent_overwrites_host_fast_model', () => {
  const hook = create(undefined, () => inventoryOf(slashConfig()))
  const output = { model: { providerID: 'anthropic', modelID: 'fast-haiku' } }
  applyHook(hook, { sessionID: 'ses_deep', agent: 'deep-coder' }, output)
  assert.equal(output.model.providerID, 'anthropic')
  assert.equal(output.model.modelID, 'deep-opus')
})

test('CHAT_PARAMS_justified_fast_agent_keeps_fast_model', () => {
  const hook = create(undefined, () => inventoryOf(slashConfig()))
  const output = { model: { providerID: 'anthropic', modelID: 'deep-opus' } }
  applyHook(hook, { sessionID: 'ses_fast', agent: 'fast-coder' }, output)
  assert.equal(output.model.modelID, 'fast-haiku')
})

test('CHAT_PARAMS_agent_less_uses_selected_deep_not_fast_default', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-chat-params-'))
  const opened = agentJournal.create({ directory: dir })
  assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
  try {
    const sid = sessionId('ses_selected_deep')
    const seeded = agentJournal.appendAgent(
      stream.session(sid),
      undefined,
      agentFact('AuthorityRootAccepted', {
        SessionId: sid,
        LogicalRunId: logicalRunId('logical-root'),
        AuthorityRootUserMessageId: authorityRoot('user-root'),
        AuthorityKind: 'AgentOwnerRoot',
        SelectedAgent: 'deep-coder',
        PeerAgent: 'fast-coder',
        CanonicalRole: 'coder',
        SelectedTier: 'deep',
      }),
      opened.journal,
    )
    assert.equal(seeded.ok, true, seeded.ok ? '' : JSON.stringify(seeded.error))

    const hook = create(opened.journal, () => inventoryOf(slashConfig()))
    const output = { model: { providerID: 'anthropic', modelID: 'fast-haiku' } }
    applyHook(hook, { sessionID: 'ses_selected_deep' }, output)
    assert.equal(output.model.modelID, 'deep-opus')
  } finally {
    opened.dispose()
    rmSync(dir, { recursive: true, force: true })
  }
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

test('CHAT_PARAMS_bare_binding_without_provider_still_pins_deep_id', () => {
  const hook = create(undefined, () => inventoryOf(bareConfig()))
  const output = {}
  applyHook(hook, { sessionID: 'ses_bare', agent: 'deep-coder' }, output)
  assert.equal(output.model, 'deep-model')
  assert.notEqual(output.model, 'fast-model')
})
