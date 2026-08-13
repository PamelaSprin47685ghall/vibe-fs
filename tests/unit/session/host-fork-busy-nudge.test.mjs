// HostForkBusyNudge must keep the handle's managed agent.
// Following the fallback Peer on an in-flight nudge is a silent Deep → Fast.

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import {
  agentFact,
  agentJournal,
  authorityRoot,
  logicalRunId,
  promptDispatcher,
  providerRun,
  resultOf,
  roles,
  sessionId,
  stream,
  transportReceipt,
} from '../support/domain.mjs'

import { send } from '../../../dist/Session/HostForkBusyNudge.js'

const capturingPort = (sends) => ({
  SubscribeTerminal: () => ({ Dispose: () => {} }),
  SendPrompt: async (session, text, options) => {
    sends.push({ session, text, options })
    return promptDispatcher.admittedWithReceipt(transportReceipt(`receipt-${sends.length}`))
  },
})

const seedDeepRoot = (journal, sid) => {
  const result = agentJournal.appendAgent(
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
    journal,
  )
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
}

const advanceToPeer = (journal, sid) => {
  const first = agentJournal.appendAgent(
    stream.session(sid),
    providerRun('asst-fail-1'),
    agentFact('FallbackCursorAdvanced', {
      SessionId: sid,
      LogicalRunId: logicalRunId('logical-root'),
      AuthorityRootUserMessageId: authorityRoot('user-root'),
      ProviderRun: providerRun('asst-fail-1'),
      PreviousOffset: 0,
      NextOffset: 1,
      ConsecutiveFailureCount: 1,
      Reason: 'unit-test',
    }),
    journal,
  )
  assert.equal(first.ok, true, first.ok ? '' : JSON.stringify(first.error))

  const second = agentJournal.appendAgent(
    stream.session(sid),
    providerRun('asst-fail-2'),
    agentFact('FallbackCursorAdvanced', {
      SessionId: sid,
      LogicalRunId: logicalRunId('logical-root'),
      AuthorityRootUserMessageId: authorityRoot('user-root'),
      ProviderRun: providerRun('asst-fail-2'),
      PreviousOffset: 1,
      NextOffset: 2,
      ConsecutiveFailureCount: 2,
      Reason: 'unit-test',
    }),
    journal,
  )
  assert.equal(second.ok, true, second.ok ? '' : JSON.stringify(second.error))
}

const withChild = async (fn) => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-busy-nudge-'))
  const opened = agentJournal.create({ directory: dir })
  assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
  try {
    const parent = sessionId('ses_parent')
    const child = sessionId('ses_child')
    seedDeepRoot(opened.journal, child)
    await fn({ journal: opened.journal, parent, child })
  } finally {
    opened.dispose()
    rmSync(dir, { recursive: true, force: true })
  }
}

test('BUSY_NUDGE_keeps_deep_handle_when_fallback_cursor_is_on_fast_peer', async () => {
  await withChild(async ({ journal, parent, child }) => {
    advanceToPeer(journal, child)
    const sends = []
    const sent = resultOf(
      await send(
        capturingPort(sends),
        parent,
        journal,
        child,
        roles.of('Coder'),
        'deep-coder',
        undefined,
        'please continue',
      ),
    )
    assert.equal(sent.ok, true, sent.error)
    assert.equal(sends.length, 1)
    assert.equal(sends[0].options.Agent, 'deep-coder')
    assert.notEqual(sends[0].options.Agent, 'fast-coder')
  })
})

test('BUSY_NUDGE_empty_agent_keeps_selected_deep_not_peer', async () => {
  await withChild(async ({ journal, parent, child }) => {
    advanceToPeer(journal, child)
    const sends = []
    const sent = resultOf(
      await send(capturingPort(sends), parent, journal, child, roles.of('Coder'), '', undefined, 'please continue'),
    )
    assert.equal(sent.ok, true, sent.error)
    assert.equal(sends[0].options.Agent, 'deep-coder')
  })
})

test('BUSY_NUDGE_explicit_peer_is_still_honored', async () => {
  await withChild(async ({ journal, parent, child }) => {
    const sends = []
    const sent = resultOf(
      await send(
        capturingPort(sends),
        parent,
        journal,
        child,
        roles.of('Coder'),
        'fast-coder',
        undefined,
        'please continue',
      ),
    )
    assert.equal(sent.ok, true, sent.error)
    assert.equal(sends[0].options.Agent, 'fast-coder')
  })
})
