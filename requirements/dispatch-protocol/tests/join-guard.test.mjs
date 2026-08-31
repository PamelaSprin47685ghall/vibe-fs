// Split from tests/unit/host/join-guard.test.mjs (cutover Wave 2a);
// owner: dispatch-protocol. R7（DISPATCH-PROTOCOL-007）：claim 释放可重试 ——
// send 失败 → Abandon(SendFailed) 释放 key，下一 nudge 可重试成功。
// JoinGuard continuation 语义断言归 interaction-authority（R12）。

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import * as joinGuard from '../../../dist/Interaction/Dispatch/JoinGuardSurface.js'

const managerRootSelection = {
  kind: 'RootSelection',
  ownerSession: null,
  ownerLogicalRun: null,
  ownerAuthorityRoot: null,
  participantIdentity: {
    selectedAgent: 'fast-manager',
    peerAgent: 'deep-manager',
    canonicalRole: 'manager',
    selectedTier: 'fast',
    persona: 'Coordinator',
    personaCatalogVersion: 1,
    origin: 'ResolvedAtRoot',
  },
}

const appendAuthorityRoot = async (handle, session) => {
  const owner = await dispatch.acceptHumanRootSelection(
    handle,
    `${session}-owner`,
    `msg-${session}-owner`,
    managerRootSelection,
  )
  assert.equal(owner.ok, true, owner.ok ? '' : owner.error)
  const inherited = authority.issueInheritedIdentitySeed('fast-coder', owner.profile)
  assert.equal(inherited.ok, true, inherited.ok ? '' : inherited.error)
  return dispatch.appendAuthorityRoot(handle, session, inherited.value)
}

const capturingPort = (captured, behaviour = {}) => ({
  SubscribeTerminal: () => ({ Dispose: () => {} }),
  SendPrompt: async (session, text, options) => {
    captured.push({ session, text, options })
    if (behaviour.failFirst && captured.length === 1) {
      return dispatch.retryable('port refused')
    }
    return dispatch.admittedWithReceipt('accepted-jg')
  },
})

test('WHAT[DISPATCH-PROTOCOL-007] JNGD_nudge_releases_the_key_when_send_fails_and_retries', async () => {
  const sid = 'ses_jg2'
  const dir = mkdtempSync(join(tmpdir(), 'wxs-jngd-'))
  const opened = await journal.JournalSurface_bootWithWriterId(dir, 'writer-jg2', 'rt-jg2', 4242, '2026-01-01T00:00:00Z')
  assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
  const appended = await appendAuthorityRoot(opened.journal, sid)
  assert.equal(appended.ok, true, appended.ok ? '' : JSON.stringify(appended.error))
  try {
    const captured = []
    const reservations = joinGuard.newReservations()
    const port = capturingPort(captured, { failFirst: true })

    const first = await joinGuard.nudge(port, opened.journal, reservations, sid, 'run-jg-1', null)
    assert.equal(first.outcome, 'NotSent', 'a definite pre-acceptance refusal must surface as NotSent')

    const second = await joinGuard.nudge(port, opened.journal, reservations, sid, 'run-jg-1', null)
    assert.equal(second.outcome, 'Sent', 'the key must be released after a failed send')
    assert.equal(captured.length, 2)
  } finally {
    try { journal.JournalSurface_dispose(opened.journal) } catch {}
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[DISPATCH-PROTOCOL-007] JNGD_join_gate_dedupes_same_terminal_but_rearms_for_fresh_terminal', async () => {
  const sid = 'ses_jg_repeat'
  const dir = mkdtempSync(join(tmpdir(), 'wxs-jngd-repeat-'))
  const opened = await journal.JournalSurface_bootWithWriterId(dir, 'writer-jg-repeat', 'rt-jg-repeat', 4242, '2026-01-01T00:00:00Z')
  assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
  assert.equal((await appendAuthorityRoot(opened.journal, sid)).ok, true)
  try {
    const captured = []
    const reservations = joinGuard.newReservations()
    const port = capturingPort(captured)

    assert.equal((await joinGuard.nudge(port, opened.journal, reservations, sid, 'run-jg-a', null)).outcome, 'Sent')
    assert.equal(
      (await joinGuard.nudge(port, opened.journal, reservations, sid, 'run-jg-a', null)).outcome,
      'AlreadyOutstanding',
      'duplicate observation of one terminal must not double-send',
    )
    assert.equal(
      (await joinGuard.nudge(port, opened.journal, reservations, sid, 'run-jg-b', null)).outcome,
      'Sent',
      'outstanding work after a fresh terminal must receive another JoinGuard reminder',
    )
    assert.equal(captured.length, 2)
  } finally {
    try { journal.JournalSurface_dispose(opened.journal) } catch {}
    rmSync(dir, { recursive: true, force: true })
  }
})
