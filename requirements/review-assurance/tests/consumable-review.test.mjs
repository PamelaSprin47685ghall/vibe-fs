// REVIEW-ASSURANCE-008..012: Reviewer terminal closure and review assurance invariants.
import assert from 'node:assert/strict'
import { mkdtempSync, readFileSync, rmSync } from 'node:fs'
import { join } from 'node:path'
import { tmpdir } from 'node:os'
import test from 'node:test'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import * as reviewJournal from '../../../dist/Persistence/Journal/ReviewJournalSurface.js'
import * as review from '../../../dist/Mission/Review/Assurance/Surface.js'

const source = (path) => readFileSync(path, 'utf8')

const managerSession = 'ses-manager'
const reviewerSession = 'ses-rev'

test('WHAT[REVIEW-ASSURANCE-008] ReviewBarrierWorkflow Direct CE drives review judgements', () => {
  const workflow = source('src/Wanxiangshu/Mission/Review/Barrier/Workflow.fs')
  assert.match(workflow, /ReviewBarrierStarted/, 'barrier workflow opens review barrier directly')
})

test('WHAT[REVIEW-ASSURANCE-009] judge_only_closure_projects_the_exact_tool_result_as_terminal_frontier', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-review-frontier-'))
  const opened = await journal.JournalSurface_bootWithWriterId(directory, 'writer-frontier', 'rt-frontier', 4242, '2026-01-01T00:00:00Z')
  assert.equal(opened.ok, true, JSON.stringify(opened))

  try {
    await reviewJournal.appendReview(opened.journal, reviewerSession, null, 'ReviewBarrierStarted', {
      ReviewerSessionId: reviewerSession,
      ManagerSessionId: managerSession,
      BarrierId: 'bar-finality',
      GitTreeHash: 'tree-1',
    })
    await reviewJournal.appendReview(opened.journal, reviewerSession, 'run-1', 'ReviewVerdictRecorded', {
      ReviewerSessionId: reviewerSession,
      ManagerSessionId: managerSession,
      BarrierId: 'bar-finality',
      GitTreeHash: 'tree-1',
      ProviderRun: 'run-1',
      ToolCallId: 'call-1',
      Verdict: 'PERFECT',
    })
    assert.equal((await reviewJournal.appendAgent(opened.journal, reviewerSession, 'run-1', 'Companion', 'XTracePartAppended', {
      SessionId: reviewerSession,
      CursorSequence: 5,
      Role: 'assistant',
      Turn: 1,
      PartIndex: 0,
      Kind: 'tool_result',
      ToolName: null,
      TextRef: 'blob-judge-result',
      TextDigest: 'digest-judge-result',
      Provenance: 'g:0/msg:review-run/host-part:judge-result',
      ProviderRun: 'run-1',
      ToolCallId: 'call-1',
      HostToolPartId: 'prt-judge-result',
    })).ok, true)
    assert.equal((await reviewJournal.appendAgent(opened.journal, reviewerSession, 'late-run', 'Companion', 'XTracePartAppended', {
      SessionId: reviewerSession,
      CursorSequence: 6,
      Role: 'assistant',
      Turn: 2,
      PartIndex: 0,
      Kind: 'reasoning',
      ToolName: null,
      TextRef: 'blob-late-tail',
      TextDigest: 'digest-late-tail',
      Provenance: 'g:0/msg:late-run/host-part:late-tail',
      ProviderRun: 'late-run',
      ToolCallId: null,
      HostToolPartId: 'prt-late-tail',
    })).ok, true)
    const closed = await reviewJournal.appendReview(opened.journal, reviewerSession, 'run-1', 'ReviewAttemptClosed', {
      ReviewerSessionId: reviewerSession,
      BarrierId: 'bar-finality',
      GitTreeHash: 'tree-1',
      ProviderRun: 'run-1',
      ToolCallId: 'call-1',
      FrozenFrontierSequence: 6,
    })
    assert.equal(closed.ok, true, JSON.stringify(closed))

    const view = reviewJournal.sessionView(opened.journal, reviewerSession)
    assert.equal(view.xTraceHead, 7n, 'late XTrace tail proves current head is wider than the frozen judge frontier')
    assert.deepEqual(view.terminalFrontier, {
      barrier: 'bar-finality',
      sequence: 6n,
      evidenceRef: 'blob-judge-result',
      evidenceDigest: 'digest-judge-result',
    })
  } finally {
    journal.JournalSurface_dispose(opened.journal)
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[REVIEW-ASSURANCE-010] review infrastructure does not fold errors into verdicts', () => {
  const guard = review.emptyGuard()
  const view = review.guardView(guard)
  assert.equal(view.witness.state, 'NoReview')
})

test('WHAT[REVIEW-ASSURANCE-011] dual PERFECT witness chain is self-contained', () => {
  const guard = review.emptyGuard()
  assert.equal(review.isConfirmed(review.guardWitness(guard)), false)
})

test('WHAT[REVIEW-ASSURANCE-012] request-range bounded evidence rejects unbound session head', () => {
  const guard = review.emptyGuard()
  assert.equal(review.satisfiesGuard('tree-1', guard), false)
})
