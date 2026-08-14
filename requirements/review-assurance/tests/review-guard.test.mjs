// RVGD: HostReviewGuard — REVIEW-003/007 guard nudges over real journal + fake port.

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync, mkdirSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import {
  agentJournal, agentFact, sessionId, logicalRunId, authorityRoot,
  stream, caseOf, promptDispatcher, transportReceipt, reviewBarrierId, gitTreeHash,
} from '../../verification-system/tests/support/domain.mjs'

const { nudgeReviewer, requestPerfectConfirmation } =
  await import('../../../dist/Infrastructure/OpenCode/Host/HostReviewGuard.js')
const { openBarrier } = await import('../../../dist/Journal/ReviewBarrier.js')
const { AgentJournalModule_appendAgent } = await import('../../../dist/Journal/AgentJournal.js')
const { SessionDirectories } = await import('../../../dist/Infrastructure/OpenCode/Host/SharedState.js')

const VERDICT_NUDGE = '# Your previous response did not submit a verdict.'

const capturingPort = (captured) => ({
  SubscribeTerminal: () => ({ Dispose: () => {} }),
  SendPrompt: async (session, text, options) => {
    captured.push({ session, text, options })
    return promptDispatcher.admittedWithReceipt(transportReceipt('accepted-rvgd'))
  },
})

const rootFact = (sid, agent = 'reviewer') =>
  agentFact('AuthorityRootAccepted', {
    SessionId: sid,
    LogicalRunId: logicalRunId(`run-${sid}`),
    AuthorityRootUserMessageId: authorityRoot(`root-${sid}`),
    AuthorityKind: 'AgentOwnerRoot',
    SelectedAgent: `fast-${agent}`,
    PeerAgent: `deep-${agent}`,
    CanonicalRole: agent,
    SelectedTier: 'fast',
  })

const outcomeName = (outcome) => outcome.cases()[outcome.tag]

const openSeeded = async (sid, agent = 'reviewer') => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-rvgd-'))
  const opened = await agentJournal.create({ directory: dir })
  assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
  const appended = await AgentJournalModule_appendAgent(stream.session(sid), undefined, rootFact(sid, agent), opened.journal)
  assert.equal(caseOf(appended), 'Ok', 'authority root must fold')
  return { opened, cleanup: () => {
    try { opened.dispose() } catch {}
    rmSync(dir, { recursive: true, force: true })
  } }
}

test('RVGD_nudgeReviewer_fails_closed_without_journal', async () => {
  const outcome = await nudgeReviewer(capturingPort([]), null, new Set(), sessionId('ses_rv'), logicalRunId('run_1'))
  assert.equal(outcomeName(outcome), 'Failed')
  assert.match(outcome.fields[0], /requires an AgentJournal/)
})

test('RVGD_nudgeReviewer_fails_without_active_authority_profile', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-rvgd-'))
  const opened = await agentJournal.create({ directory: dir })
  assert.equal(opened.ok, true)
  try {
    const outcome = await nudgeReviewer(capturingPort([]), opened.journal, new Set(), sessionId('ses_rv'), logicalRunId('run_1'))
    assert.equal(outcomeName(outcome), 'Failed')
    assert.match(outcome.fields[0], /No active authority profile/)
  } finally {
    opened.dispose()
    rmSync(dir, { recursive: true, force: true })
  }
})

test('RVGD_nudgeReviewer_sends_verdict_guard_then_dedupes', async () => {
  const sid = sessionId('ses_rv1')
  const { opened, cleanup } = await openSeeded(sid)
  try {
    const captured = []
    const first = await nudgeReviewer(capturingPort(captured), opened.journal, new Set(), sid, logicalRunId('run_1'))
    assert.equal(outcomeName(first), 'Sent', JSON.stringify(first.fields?.[0]))
    assert.ok(first.fields[0], 'Sent carries a PromptKey')
    assert.equal(captured.length, 1)
    assert.ok(captured[0].text.startsWith(VERDICT_NUDGE), `verdict guard prompt expected: ${captured[0].text}`)
    assert.equal(captured[0].session, sid)

    const second = await nudgeReviewer(capturingPort([]), opened.journal, new Set(), sid, logicalRunId('run_1'))
    assert.equal(outcomeName(second), 'AlreadyOutstanding', 'durable claim must suppress a second nudge')
  } finally {
    cleanup()
  }
})

test('RVGD_requestPerfectConfirmation_sends_review_confirmation_challenge', async () => {
  const sid = sessionId('ses_rv2')
  const { opened, cleanup } = await openSeeded(sid)
  try {
    const captured = []
    const first = await requestPerfectConfirmation(capturingPort(captured), opened.journal, new Set(), sid, logicalRunId('run_2'))
    assert.equal(outcomeName(first), 'Sent', JSON.stringify(first.fields?.[0]))
    assert.ok(first.fields[0])
    assert.equal(captured.length, 1)
    assert.ok(captured[0].text.length > 0, 'confirmation prompt must be non-empty')
    assert.notEqual(captured[0].text, VERDICT_NUDGE, 'confirmation uses the rendered challenge, not the verdict guard')

    const second = await requestPerfectConfirmation(capturingPort([]), opened.journal, new Set(), sid, logicalRunId('run_2'))
    assert.equal(outcomeName(second), 'AlreadyOutstanding')
  } finally {
    cleanup()
  }
})

test('RVGD_nudgeReviewer_no_longer_required_when_recorded_worktree_is_dead', async () => {
  const sid = sessionId('ses_rv3')
  const { opened, cleanup } = await openSeeded(sid)
  const worktree = mkdtempSync(join(tmpdir(), 'wxs-rvgd-wt-'))
  SessionDirectories.set('ses_rv3', worktree)
  try {
    const captured = []
    const outcome = await nudgeReviewer(capturingPort(captured), opened.journal, new Set(), sid, logicalRunId('run_3'))
    assert.equal(outcomeName(outcome), 'NoLongerRequired', 'a recorded worktree without AGENTS.md is dead')
    assert.equal(captured.length, 0, 'no prompt may be sent to a dead worktree')
  } finally {
    SessionDirectories.delete('ses_rv3')
    rmSync(worktree, { recursive: true, force: true })
    cleanup()
  }
})

test('RVGD_nudgeReviewer_sends_when_recorded_worktree_is_alive', async () => {
  const sid = sessionId('ses_rv4')
  const { opened, cleanup } = await openSeeded(sid)
  const worktree = mkdtempSync(join(tmpdir(), 'wxs-rvgd-wt-'))
  writeFileSync(join(worktree, 'AGENTS.md'), 'instructions')
  SessionDirectories.set('ses_rv4', worktree)
  try {
    const captured = []
    const outcome = await nudgeReviewer(capturingPort(captured), opened.journal, new Set(), sid, logicalRunId('run_4'))
    assert.equal(outcomeName(outcome), 'Sent', JSON.stringify(outcome.fields?.[0]))
    assert.equal(captured.length, 1)
  } finally {
    SessionDirectories.delete('ses_rv4')
    rmSync(worktree, { recursive: true, force: true })
    cleanup()
  }
})

test('RVGD_openBarrier_is_the_shared_review_barrier_writer', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-rvgd-'))
  const opened = await agentJournal.create({ directory: dir })
  assert.equal(opened.ok, true)
  try {
    const barrier = await openBarrier(opened.journal, sessionId('ses_mgr'), sessionId('ses_rv5'), reviewBarrierId('bar_1'), gitTreeHash('tree_1'))
    assert.equal(barrier.tag, 0, `barrier must open: ${JSON.stringify(barrier)}`)
  } finally {
    opened.dispose()
    rmSync(dir, { recursive: true, force: true })
  }
})
