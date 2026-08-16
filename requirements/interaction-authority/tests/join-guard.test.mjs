// Split from tests/unit/host/join-guard.test.mjs (cutover Wave 2a);
// owner: interaction-authority. JNGD: HostJoinGuard continuation 语义半边
// （INTERACTION-AUTHORITY-014 / R12）—— join-capable 角色必须 join outstanding
// work：无 journal fail-closed、无 active authority profile fail-closed、
// 发送 join-guard continuation 并持久化 claim（claim 持久化半边归
// dispatch-protocol）。send 失败释放 key 的断言归 dispatch-protocol（R7）。

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import {
  agentJournal, agentFact, sessionId, logicalRunId, authorityRoot,
  stream, caseOf, promptDispatcher, transportReceipt, runtimeNudge,
} from '../../verification-system/tests/support/domain.mjs'

const { nudge } = await import('../../../dist/Execution/Delegation/Fork/OpenCode/JoinGuard.js')
const { AgentJournalModule_appendAgent } = await import('../../../dist/Persistence/Journal/AgentJournal.js')

const capturingPort = (captured) => ({
  SubscribeTerminal: () => ({ Dispose: () => {} }),
  SendPrompt: async (session, text, options) => {
    captured.push({ session, text, options })
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

const outcomeName = (outcome) => outcome.cases()[outcome.tag]

const openSeeded = async (sid) => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-jngd-'))
  const opened = await agentJournal.create({ directory: dir })
  assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
  const appended = await AgentJournalModule_appendAgent(stream.session(sid), undefined, rootFact(sid), opened.journal)
  assert.equal(caseOf(appended), 'Ok', 'authority root must fold')
  return { opened, dir, cleanup: () => {
    try { opened.dispose() } catch {}
    rmSync(dir, { recursive: true, force: true })
  } }
}

test('WHAT[INTERACTION-AUTHORITY-014] JNGD_nudge_fails_closed_without_journal', async () => {
  const outcome = await nudge(capturingPort([]), null, new Set(), sessionId('ses_jg'), undefined)
  assert.equal(outcomeName(outcome), 'Failed')
  assert.match(outcome.fields[0], /requires an AgentJournal/)
})

test('WHAT[INTERACTION-AUTHORITY-014] JNGD_nudge_fails_without_active_authority_profile', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-jngd-'))
  const opened = await agentJournal.create({ directory: dir })
  assert.equal(opened.ok, true)
  try {
    const outcome = await nudge(capturingPort([]), opened.journal, new Set(), sessionId('ses_jg'), undefined)
    assert.equal(outcomeName(outcome), 'Failed')
    assert.match(outcome.fields[0], /No active authority profile/)
  } finally {
    opened.dispose()
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[INTERACTION-AUTHORITY-014] JNGD_nudge_sends_join_guard_continuation_and_claims', async () => {
  const sid = sessionId('ses_jg1')
  const { opened, cleanup } = await openSeeded(sid)
  try {
    const captured = []
    const outcome = await nudge(capturingPort(captured), opened.journal, new Set(), sid, undefined)
    assert.equal(outcomeName(outcome), 'Sent', JSON.stringify(outcome.fields?.[0]))
    assert.ok(outcome.fields[0], 'Sent carries a PromptKey')
    assert.equal(captured.length, 1)
    assert.ok(
      captured[0].text.startsWith(runtimeNudge.backgroundJoinGuard),
      `prompt must be the join-guard nudge: ${captured[0].text}`,
    )
    assert.equal(captured[0].session, sid)

    // The claim is durable: a second nudge is AlreadyOutstanding.
    const again = await nudge(capturingPort([]), opened.journal, new Set(), sid, undefined)
    assert.equal(outcomeName(again), 'AlreadyOutstanding')
  } finally {
    cleanup()
  }
})
