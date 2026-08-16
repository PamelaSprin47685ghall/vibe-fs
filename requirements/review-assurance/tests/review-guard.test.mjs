// RVGD: Host review guard owner surface. Missing-verdict and confirmation
// nudges use a durable JournalHandle and plain transport outcomes.
import assert from 'node:assert/strict'
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import * as reviewJournal from '../../../dist/Persistence/Journal/ReviewJournalSurface.js'
import * as reviewHost from '../../../dist/Mission/Review/OpenCode/ReviewHostSurface.js'

const VERDICT_NUDGE = '# Your previous response did not call judge.'

const capturingPort = (captured, physical = false) => ({
  SubscribeTerminal: () => ({ Dispose: () => {} }),
  SendPrompt: async (session, text, options) => {
    captured.push({ session, text, options })
    return physical ? reviewHost.admittedWithPhysicalMessage(`accepted-rvgd-${captured.length}`) : reviewHost.admittedWithReceipt('accepted-rvgd')
  },
})

const openSeeded = async (sid, agent = 'reviewer') => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-rvgd-'))
  const opened = await journal.JournalSurface_bootWithWriterId(dir, `writer-${sid}`, 'rt-rvgd', 4242, '2026-01-01T00:00:00Z')
  assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
  const root = await reviewJournal.appendAuthorityRoot(opened.journal, sid, agent)
  assert.equal(root.ok, true, root.ok ? '' : JSON.stringify(root.error))
  const barrier = await reviewHost.openBarrier(opened.journal, 'ses_mgr_rvgd', sid, `bar-${sid}`, `tree-${sid}`)
  assert.equal(barrier.ok, true, barrier.ok ? '' : JSON.stringify(barrier.error))
  return {
    opened,
    cleanup: () => {
      try { journal.JournalSurface_dispose(opened.journal) } catch {}
      rmSync(dir, { recursive: true, force: true })
      reviewHost.clearGuardNudges()
    },
  }
}

test('WHAT[REVIEW-ASSURANCE-010] RVGD_nudgeReviewer_fails_closed_without_journal', async () => {
  reviewHost.clearGuardNudges()
  const outcome = await reviewHost.nudgeReviewer(capturingPort([]), null, 'ses_rv')
  assert.equal(outcome.outcome, 'Failed')
  assert.match(outcome.reason, /requires an AgentJournal/)
})

test('WHAT[REVIEW-ASSURANCE-010] RVGD_nudgeReviewer_fails_without_open_review_barrier', async () => {
  reviewHost.clearGuardNudges()
  const dir = mkdtempSync(join(tmpdir(), 'wxs-rvgd-'))
  const opened = await journal.JournalSurface_bootWithWriterId(dir, 'writer-no-barrier', 'rt-rvgd', 4242, '2026-01-01T00:00:00Z')
  const sid = 'ses_rv_no_barrier'
  try {
    const root = await reviewJournal.appendAuthorityRoot(opened.journal, sid)
    assert.equal(root.ok, true)
    const captured = []
    const outcome = await reviewHost.nudgeReviewer(capturingPort(captured), opened.journal, sid)
    assert.equal(outcome.outcome, 'Failed')
    assert.match(outcome.reason, /open review barrier/)
    assert.equal(captured.length, 0)
  } finally {
    journal.JournalSurface_dispose(opened.journal)
    rmSync(dir, { recursive: true, force: true })
    reviewHost.clearGuardNudges()
  }
})

test('WHAT[REVIEW-ASSURANCE-010] RVGD_nudgeReviewer_fails_without_active_authority_profile', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-rvgd-'))
  const opened = await journal.JournalSurface_bootWithWriterId(dir, 'writer-no-profile', 'rt-rvgd', 4242, '2026-01-01T00:00:00Z')
  try {
    const barrier = await reviewHost.openBarrier(opened.journal, 'ses_mgr_rvgd', 'ses_rv', 'bar-ses_rv', 'tree-ses_rv')
    assert.equal(barrier.ok, true)
    const outcome = await reviewHost.nudgeReviewer(capturingPort([]), opened.journal, 'ses_rv')
    assert.equal(outcome.outcome, 'Failed')
    assert.match(outcome.reason, /No active authority profile/)
  } finally {
    journal.JournalSurface_dispose(opened.journal)
    rmSync(dir, { recursive: true, force: true })
    reviewHost.clearGuardNudges()
  }
})

test('WHAT[REVIEW-ASSURANCE-001] RVGD_nudgeReviewer_sends_verdict_guard_then_dedupes', async () => {
  const sid = 'ses_rv1'
  const { opened, cleanup } = await openSeeded(sid)
  try {
    const captured = []
    const first = await reviewHost.nudgeReviewer(capturingPort(captured), opened.journal, sid)
    assert.equal(first.outcome, 'Sent')
    assert.ok(first.promptKey)
    assert.equal(captured.length, 1)
    assert.ok(captured[0].text.startsWith(VERDICT_NUDGE))
    assert.match(captured[0].text, /call judge exactly once/)
    assert.match(captured[0].text, /verdict set to PERFECT or REVISE/)
    assert.doesNotMatch(captured[0].text, /verdict tool/)
    assert.equal(captured[0].session, sid)
    const second = await reviewHost.nudgeReviewer(capturingPort([]), opened.journal, sid)
    assert.equal(second.outcome, 'AlreadyOutstanding')
  } finally { cleanup() }
})

test('WHAT[REVIEW-ASSURANCE-010] RVGD_nudgeReviewer_cross_instance_reservation_suppresses_twin_send', async () => {
  const sid = 'ses_rv_xinst'
  const a = await openSeeded(sid)
  const bDir = mkdtempSync(join(tmpdir(), 'wxs-rvgd-b-'))
  const b = await journal.JournalSurface_bootWithWriterId(bDir, 'writer-twin', 'rt-rvgd', 4243, '2026-01-01T00:00:00Z')
  await reviewJournal.appendAuthorityRoot(b.journal, sid)
  await reviewHost.openBarrier(b.journal, 'ses_mgr_rvgd', sid, `bar-${sid}`, `tree-${sid}`)
  try {
    const captured = []
    const first = await reviewHost.nudgeReviewer(capturingPort(captured), a.opened.journal, sid)
    assert.equal(first.outcome, 'Sent')
    const twin = await reviewHost.nudgeReviewer(capturingPort(captured), b.journal, sid)
    assert.equal(twin.outcome, 'AlreadyOutstanding')
    assert.equal(captured.length, 1)
  } finally {
    journal.JournalSurface_dispose(b.journal)
    rmSync(bDir, { recursive: true, force: true })
    a.cleanup()
  }
})

test('WHAT[REVIEW-ASSURANCE-006] RVGD_nudgeReviewer_new_barrier_receives_a_fresh_single_repair_budget', async () => {
  const sid = 'ses_rv_rearm'
  const { opened, cleanup } = await openSeeded(sid)
  try {
    const captured = []
    assert.equal((await reviewHost.nudgeReviewer(capturingPort(captured, true), opened.journal, sid)).outcome, 'Sent')
    const next = await reviewHost.openBarrier(opened.journal, 'ses_mgr_rvgd', sid, `${sid}-next`, `${sid}-next`)
    assert.equal(next.ok, true)
    assert.equal((await reviewHost.nudgeReviewer(capturingPort(captured, true), opened.journal, sid)).outcome, 'Sent')
    assert.equal(captured.length, 2)
  } finally { cleanup() }
})

test('WHAT[REVIEW-ASSURANCE-010] RVGD_nudgeReviewer_no_longer_required_when_recorded_worktree_is_dead', async () => {
  const sid = 'ses_rv3'
  const { opened, cleanup } = await openSeeded(sid)
  const worktree = mkdtempSync(join(tmpdir(), 'wxs-rvgd-wt-'))
  reviewHost.setSessionDirectory(sid, worktree)
  try {
    const captured = []
    assert.equal((await reviewHost.nudgeReviewer(capturingPort(captured), opened.journal, sid)).outcome, 'NoLongerRequired')
    assert.equal(captured.length, 0)
  } finally { reviewHost.clearSessionDirectory(sid); rmSync(worktree, { recursive: true, force: true }); cleanup() }
})

test('WHAT[REVIEW-ASSURANCE-010] RVGD_nudgeReviewer_sends_when_recorded_worktree_is_alive', async () => {
  const sid = 'ses_rv4'
  const { opened, cleanup } = await openSeeded(sid)
  const worktree = mkdtempSync(join(tmpdir(), 'wxs-rvgd-wt-'))
  writeFileSync(join(worktree, 'AGENTS.md'), 'instructions')
  reviewHost.setSessionDirectory(sid, worktree)
  try {
    assert.equal((await reviewHost.nudgeReviewer(capturingPort([]), opened.journal, sid)).outcome, 'Sent')
  } finally { reviewHost.clearSessionDirectory(sid); rmSync(worktree, { recursive: true, force: true }); cleanup() }
})

test('WHAT[REVIEW-ASSURANCE-006] RVGD_openBarrier_is_the_shared_review_barrier_writer', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-rvgd-'))
  const opened = await journal.JournalSurface_bootWithWriterId(dir, 'writer-barrier', 'rt-rvgd', 4242, '2026-01-01T00:00:00Z')
  try {
    const barrier = await reviewHost.openBarrier(opened.journal, 'ses_mgr', 'ses_rv5', 'bar_1', 'tree_1')
    assert.equal(barrier.ok, true, JSON.stringify(barrier))
  } finally { journal.JournalSurface_dispose(opened.journal); rmSync(dir, { recursive: true, force: true }) }
})
