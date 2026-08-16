// RVGD: HostReviewGuard — REVIEW-003/007 guard nudges over real journal + fake port.
// HOST-012: reservation lives in SharedState.ReviewGuardNudges (no RuntimeId) so
// root + worktree plugin instances cannot both deliver ReviewerVerdictRequired.

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import {
  agentJournal, agentFact, sessionId, logicalRunId, authorityRoot,
  stream, caseOf, promptDispatcher, transportReceipt, reviewBarrierId, gitTreeHash,
} from '../../verification-system/tests/support/domain.mjs'

const { nudgeReviewer, requestPerfectConfirmation } =
  await import('../../../dist/Mission/Review/OpenCode/HostGuard.js')
const { openBarrier } = await import('../../../dist/Mission/Review/Barrier/Workflow.js')
const { AgentJournalModule_appendAgent } = await import('../../../dist/Persistence/Journal/AgentJournal.js')
const { SessionDirectories, clearReviewGuardNudgesForTests } = await import('../../../dist/OpenCode/Host/SharedState.js')

const VERDICT_NUDGE = '# Your previous response did not call judge.'

const capturingPort = (captured) => ({
  SubscribeTerminal: () => ({ Dispose: () => {} }),
  SendPrompt: async (session, text, options) => {
    captured.push({ session, text, options })
    return promptDispatcher.admittedWithReceipt(transportReceipt('accepted-rvgd'))
  },
})

const acceptingPort = (captured) => ({
  SubscribeTerminal: () => ({ Dispose: () => {} }),
  SendPrompt: async (session, text, options) => {
    captured.push({ session, text, options })
    return promptDispatcher.admittedWithPhysicalMessage(`accepted-rvgd-${captured.length}`)
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

const clearGuardNudges = () => clearReviewGuardNudgesForTests()
const sidValue = (sid) => sid?.fields?.[0] ?? sid
const barrierFor = (sid) => reviewBarrierId(`bar-${sidValue(sid)}`)

const seedBarrier = async (journal, sid) => {
  const barrier = await openBarrier(
    journal,
    sessionId('ses_mgr_rvgd'),
    sid,
    barrierFor(sid),
    gitTreeHash(`tree-${sidValue(sid)}`),
  )
  assert.equal(barrier.tag, 0, 'review barrier must be durable before a missing-verdict nudge')
}

const openSeeded = async (sid, agent = 'reviewer') => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-rvgd-'))
  const opened = await agentJournal.create({ directory: dir })
  assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
  const appended = await AgentJournalModule_appendAgent(stream.session(sid), undefined, rootFact(sid, agent), opened.journal)
  assert.equal(caseOf(appended), 'Ok', 'authority root must fold')
  await seedBarrier(opened.journal, sid)
  return { opened, cleanup: () => {
    try { opened.dispose() } catch {}
    rmSync(dir, { recursive: true, force: true })
    clearGuardNudges()
  } }
}

test('WHAT[REVIEW-ASSURANCE-010] RVGD_nudgeReviewer_fails_closed_without_journal', async () => {
  clearGuardNudges()
  const outcome = await nudgeReviewer(capturingPort([]), null, sessionId('ses_rv'))
  assert.equal(outcomeName(outcome), 'Failed')
  assert.match(outcome.fields[0], /requires an AgentJournal/)
})

test('WHAT[REVIEW-ASSURANCE-010] RVGD_nudgeReviewer_fails_without_open_review_barrier', async () => {
  clearGuardNudges()
  const dir = mkdtempSync(join(tmpdir(), 'wxs-rvgd-'))
  const opened = await agentJournal.create({ directory: dir })
  assert.equal(opened.ok, true)
  const sid = sessionId('ses_rv_no_barrier')
  try {
    const appended = await AgentJournalModule_appendAgent(stream.session(sid), undefined, rootFact(sid), opened.journal)
    assert.equal(caseOf(appended), 'Ok')
    const captured = []
    const outcome = await nudgeReviewer(capturingPort(captured), opened.journal, sid)
    assert.equal(outcomeName(outcome), 'Failed')
    assert.match(outcome.fields[0], /open review barrier/)
    assert.equal(captured.length, 0, 'an unscoped reviewer repair must never be sent')
  } finally {
    opened.dispose()
    rmSync(dir, { recursive: true, force: true })
    clearGuardNudges()
  }
})

test('WHAT[REVIEW-ASSURANCE-010] RVGD_nudgeReviewer_fails_without_active_authority_profile', async () => {
  clearGuardNudges()
  const dir = mkdtempSync(join(tmpdir(), 'wxs-rvgd-'))
  const opened = await agentJournal.create({ directory: dir })
  assert.equal(opened.ok, true)
  try {
    const sid = sessionId('ses_rv')
    await seedBarrier(opened.journal, sid)
    const outcome = await nudgeReviewer(capturingPort([]), opened.journal, sid)
    assert.equal(outcomeName(outcome), 'Failed')
    assert.match(outcome.fields[0], /No active authority profile/)
  } finally {
    opened.dispose()
    rmSync(dir, { recursive: true, force: true })
    clearGuardNudges()
  }
})

test('WHAT[REVIEW-ASSURANCE-001] RVGD_nudgeReviewer_sends_verdict_guard_then_dedupes', async () => {
  const sid = sessionId('ses_rv1')
  const { opened, cleanup } = await openSeeded(sid)
  try {
    const captured = []
    const first = await nudgeReviewer(capturingPort(captured), opened.journal, sid)
    assert.equal(outcomeName(first), 'Sent', JSON.stringify(first.fields?.[0]))
    assert.ok(first.fields[0], 'Sent carries a PromptKey')
    assert.equal(captured.length, 1)
    assert.ok(captured[0].text.startsWith(VERDICT_NUDGE), `verdict guard prompt expected: ${captured[0].text}`)
    assert.match(captured[0].text, /call judge exactly once/, 'repair prompt must command the actual Reviewer judgement tool')
    assert.match(captured[0].text, /verdict set to PERFECT or REVISE/, 'repair prompt must state the typed verdict argument')
    assert.match(captured[0].text, /Do not substitute prose for the tool call/, 'repair prompt must reject prose-only pseudo-submission')
    assert.doesNotMatch(captured[0].text, /verdict tool/, 'legacy nonexistent verdict tool name must never be emitted')
    assert.equal(captured[0].session, sid)

    const second = await nudgeReviewer(capturingPort([]), opened.journal, sid)
    assert.equal(outcomeName(second), 'AlreadyOutstanding', 'shared reservation must suppress a second nudge')
  } finally {
    cleanup()
  }
})

test('WHAT[REVIEW-ASSURANCE-010] RVGD_nudgeReviewer_cross_instance_reservation_suppresses_twin_send', async () => {
  // Two journals = two plugin instances. The reservation is keyed by the durable
  // review barrier, not RuntimeId and not the provider run that happened to expose
  // the missing judge. Both instances therefore compete for one logical repair.
  const sid = sessionId('ses_rv_xinst')
  const a = await openSeeded(sid, 'reviewer')
  const bDir = mkdtempSync(join(tmpdir(), 'wxs-rvgd-b-'))
  const bOpened = await agentJournal.create({ directory: bDir })
  assert.equal(bOpened.ok, true)
  const bAppended = await AgentJournalModule_appendAgent(
    stream.session(sid),
    undefined,
    rootFact(sid, 'reviewer'),
    bOpened.journal,
  )
  assert.equal(caseOf(bAppended), 'Ok')
  await seedBarrier(bOpened.journal, sid)
  try {
    const captured = []
    const first = await nudgeReviewer(capturingPort(captured), a.opened.journal, sid)
    assert.equal(outcomeName(first), 'Sent', JSON.stringify(first.fields?.[0]))
    assert.equal(captured.length, 1)

    const twin = await nudgeReviewer(capturingPort(captured), bOpened.journal, sid)
    assert.equal(outcomeName(twin), 'AlreadyOutstanding', 'same durable review barrier must dedupe across plugin instances; provider run identity is not part of the missing-verdict occasion')
    assert.equal(captured.length, 1, 'exactly one physical SendPrompt for one missing-verdict occasion')
  } finally {
    try { bOpened.dispose() } catch {}
    rmSync(bDir, { recursive: true, force: true })
    a.cleanup()
  }
})

test('WHAT[REVIEW-ASSURANCE-006] RVGD_nudgeReviewer_new_barrier_receives_a_fresh_single_repair_budget', async () => {
  const sid = sessionId('ses_rv_rearm')
  const { opened, cleanup } = await openSeeded(sid)
  try {
    const captured = []
    const first = await nudgeReviewer(acceptingPort(captured), opened.journal, sid)
    assert.equal(outcomeName(first), 'Sent')
    assert.equal(captured.length, 1)

    const nextBarrier = await openBarrier(
      opened.journal,
      sessionId('ses_mgr_rvgd'),
      sid,
      reviewBarrierId('bar-ses_rv_rearm-next'),
      gitTreeHash('tree-ses_rv_rearm-next'),
    )
    assert.equal(nextBarrier.tag, 0)

    const second = await nudgeReviewer(acceptingPort(captured), opened.journal, sid)
    assert.equal(outcomeName(second), 'Sent', 'a new durable review requirement must receive its own one repair budget')
    assert.equal(captured.length, 2)
  } finally {
    cleanup()
  }
})

test('WHAT[REVIEW-ASSURANCE-002] RVGD_requestPerfectConfirmation_sends_review_confirmation_challenge', async () => {
  const sid = sessionId('ses_rv2')
  const { opened, cleanup } = await openSeeded(sid)
  try {
    const captured = []
    const first = await requestPerfectConfirmation(capturingPort(captured), opened.journal, sid, logicalRunId('run_2'))
    assert.equal(outcomeName(first), 'Sent', JSON.stringify(first.fields?.[0]))
    assert.ok(first.fields[0])
    assert.equal(captured.length, 1)
    assert.ok(captured[0].text.length > 0, 'confirmation prompt must be non-empty')
    assert.notEqual(captured[0].text, VERDICT_NUDGE, 'confirmation uses the rendered challenge, not the verdict guard')

    const second = await requestPerfectConfirmation(capturingPort([]), opened.journal, sid, logicalRunId('run_2'))
    assert.equal(outcomeName(second), 'AlreadyOutstanding')
  } finally {
    cleanup()
  }
})

test('WHAT[REVIEW-ASSURANCE-010] RVGD_nudgeReviewer_no_longer_required_when_recorded_worktree_is_dead', async () => {
  const sid = sessionId('ses_rv3')
  const { opened, cleanup } = await openSeeded(sid)
  const worktree = mkdtempSync(join(tmpdir(), 'wxs-rvgd-wt-'))
  SessionDirectories.set('ses_rv3', worktree)
  try {
    const captured = []
    const outcome = await nudgeReviewer(capturingPort(captured), opened.journal, sid)
    assert.equal(outcomeName(outcome), 'NoLongerRequired', 'a recorded worktree without AGENTS.md is dead')
    assert.equal(captured.length, 0, 'no prompt may be sent to a dead worktree')
  } finally {
    SessionDirectories.delete('ses_rv3')
    rmSync(worktree, { recursive: true, force: true })
    cleanup()
  }
})

test('WHAT[REVIEW-ASSURANCE-010] RVGD_nudgeReviewer_sends_when_recorded_worktree_is_alive', async () => {
  const sid = sessionId('ses_rv4')
  const { opened, cleanup } = await openSeeded(sid)
  const worktree = mkdtempSync(join(tmpdir(), 'wxs-rvgd-wt-'))
  writeFileSync(join(worktree, 'AGENTS.md'), 'instructions')
  SessionDirectories.set('ses_rv4', worktree)
  try {
    const captured = []
    const outcome = await nudgeReviewer(capturingPort(captured), opened.journal, sid)
    assert.equal(outcomeName(outcome), 'Sent', JSON.stringify(outcome.fields?.[0]))
    assert.equal(captured.length, 1)
  } finally {
    SessionDirectories.delete('ses_rv4')
    rmSync(worktree, { recursive: true, force: true })
    cleanup()
  }
})

test('WHAT[REVIEW-ASSURANCE-006] RVGD_openBarrier_is_the_shared_review_barrier_writer', async () => {
  clearGuardNudges()
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
