// Split from tests/unit/host/join-guard.test.mjs (cutover Wave 2a);
// owner: dispatch-protocol. R7（DISPATCH-PROTOCOL-007）：claim 释放可重试 ——
// send 失败 → Abandon(SendFailed) 释放 key，下一 nudge 可重试成功。
// JoinGuard continuation 语义断言归 interaction-authority（R12）。

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import {
  agentJournal, agentFact, sessionId, logicalRunId, authorityRoot,
  stream, promptDispatcher, transportReceipt,
} from '../../verification-system/tests/support/domain.mjs'

const { nudge } = await import('../../../dist/Execution/Delegation/Fork/OpenCode/JoinGuard.js')

const capturingPort = (captured, behaviour = {}) => ({
  SubscribeTerminal: () => ({ Dispose: () => {} }),
  SendPrompt: async (session, text, options) => {
    captured.push({ session, text, options })
    if (behaviour.failFirst && captured.length === 1) {
      return { tag: 2, fields: ['port refused'] }
    }
    return promptDispatcher.admittedWithReceipt(transportReceipt('accepted-jg'))
  },
})

const rootFact = (sid) =>
  agentFact('AuthorityRootAccepted', {
    SessionId: sid,
    LogicalRunId: logicalRunId(`run-${sid}`),
    AuthorityRootUserMessageId: authorityRoot(`root-${sid}`),
    AuthorityKind: 'AgentOwnerRoot',
    SelectedAgent: 'fast-coder',
    PeerAgent: 'deep-coder',
    CanonicalRole: 'coder',
    SelectedTier: 'fast',
  })

const outcomeName = (outcome) => outcome.name

test('WHAT[DISPATCH-PROTOCOL-007] JNGD_nudge_releases_the_key_when_send_fails_and_retries', async () => {
  const sid = sessionId('ses_jg2')
  const dir = mkdtempSync(join(tmpdir(), 'wxs-jngd-'))
  const opened = await agentJournal.create({ directory: dir })
  assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
  const appended = await agentJournal.appendAgent(stream.session(sid), undefined, rootFact(sid), opened.journal)
  assert.equal(appended.ok, true, 'authority root must fold')
  try {
    const captured = []
    const port = capturingPort(captured, { failFirst: true })

    const first = await nudge(port, opened.journal, new Set(), sid, undefined)
    assert.equal(outcomeName(first), 'Failed', 'a refused port must surface as Failed')

    const second = await nudge(port, opened.journal, new Set(), sid, undefined)
    assert.equal(outcomeName(second), 'Sent', 'the key must be released after a failed send')
    assert.equal(captured.length, 2)
  } finally {
    try { opened.dispose() } catch {}
    rmSync(dir, { recursive: true, force: true })
  }
})
