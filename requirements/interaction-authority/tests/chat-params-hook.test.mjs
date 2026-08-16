// Split from tests/unit/host/chat-params-hook.test.mjs (cutover Wave 2a);
// owner: interaction-authority. chat.params binding 语义半边：parented session
// 必须有 provider message binding（否则 fail-closed throw，且 adapter 不重写
// Host output）；agent-less root 不从 journal 发明 binding。
// 观察适配 no-op 断言归 host-boundary。

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import { dirname } from 'node:path'

const here = dirname(fileURLToPath(import.meta.url))
const { agentFact, agentJournal, authorityRoot, logicalRunId, resultOf, sessionId, stream } = await import(
  '../../verification-system/tests/support/domain.mjs'
)

const { create } = await import(join(here, '../../../dist/OpenCode/Host/ChatParamsHook.js'))
const { validate } = await import(join(here, '../../../dist/OpenCode/Host/ManagedAgentConfig.js'))
const sessionsModule = await import(join(here, '../../../dist/OpenCode/Host/Sessions.js'))
const createSessionPort = Object.entries(sessionsModule).find(([k]) => k.startsWith('InjectedSessionPort_$ctor'))?.[1]

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

const inventoryOf = (config) => {
  const parsed = resultOf(validate(config))
  assert.equal(parsed.ok, true, parsed.ok ? '' : parsed.error)
  return parsed.value
}

const applyHook = (hook, input, output) => {
  const next = hook(input, output)
  if (typeof next === 'function') next(output)
}

test('WHAT[INTERACTION-AUTHORITY-011] CHAT_PARAMS_parented_session_requires_provider_message_binding', async () => {
  const child = sessionId('ses_chat_params_child')
  const sessions = createSessionPort(
    { CreateChildSession: async () => ({ tag: 0, fields: [child] }) },
    { SubscribeTerminalListener: () => ({ Dispose: () => {} }) },
  )
  const created = await sessions.CreateChildSession(sessionId('ses_chat_params_root'), { Agent: 'deep-coder' })
  assert.equal(created.tag, 0)

  const hook = create()
  const output = { model: { providerID: 'anthropic', modelID: 'fast-haiku' } }
  assert.throws(
    () => applyHook(hook, { sessionID: 'ses_chat_params_child', agent: 'fast-coder' }, output),
    /no observable provider\/model binding/,
  )
  assert.equal(output.model.modelID, 'fast-haiku', 'chat.params never rewrites Host output')
})

test('WHAT[INTERACTION-AUTHORITY-011] CHAT_PARAMS_agent_less_root_does_not_invent_binding_from_journal', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-chat-params-'))
  const opened = await agentJournal.create({ directory: dir })
  assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
  try {
    const sid = sessionId('ses_selected_deep')
    const seeded = await agentJournal.appendAgent(
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
    assert.equal(output.model.modelID, 'fast-haiku')
  } finally {
    opened.dispose()
    rmSync(dir, { recursive: true, force: true })
  }
})
