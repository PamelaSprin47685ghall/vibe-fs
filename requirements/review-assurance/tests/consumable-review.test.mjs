// REVIEW-014/017/018: ConsumableReview is a durable Todo marker, not a
// provider-visible verdict. Magic Todo and Review journal capabilities cross only
// through their owner surfaces.
import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { join } from 'node:path'
import { tmpdir } from 'node:os'
import test from 'node:test'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import * as reviewJournal from '../../../dist/Persistence/Journal/ReviewJournalSurface.js'
import * as review from '../../../dist/Mission/Review/Assurance/Surface.js'
import * as todo from '../../../dist/Mission/Review/ReviewTodoSurface.js'

const sha256 = (value) => `digest:${value}`
const life = 'life-consumable'
const managerSession = 'ses-manager'
const reviewerSession = 'ses-rev'
const ids = todo.ids(sha256, life, 'todo-call')
const write = ids.todoWriteId
const reviewId = ids.todoReviewId
const dedicated = ids.dedicatedReviewerId
const prepared = {
  ManagerSessionId: managerSession,
  ManagerLifeId: life,
  TodoWriteId: write,
  ToolCallId: 'todo-call',
  ToolPartOrdinal: 2,
  BaseTodoRef: 'base-list',
  BaseTodoDigest: 'base-digest',
  ProposedTodoRef: 'proposal-list',
  ProposedTodoDigest: 'proposal-digest',
  PlanCompleteDeclared: true,
  ProviderInputDigest: 'provider-input-digest',
  ReviewFrontier: { Sequence: 10 },
  SemanticVersion: 'magic-v1',
}
const accepted = {
  ManagerLifeId: life,
  TodoWriteId: write,
  ToolCallId: 'todo-call',
  PreparedFactRef: 'prepared-fact-ref',
  InputDigest: 'provider-input-digest',
  OutputDigest: 'output-digest',
  PhysicalSuccessEvidence: 'LiveAfterSuccess',
  SemanticVersion: 'magic-v1',
}
const enlisted = { ManagerLifeId: life, DedicatedReviewerId: dedicated, ReviewerSessionId: reviewerSession }
const assigned = {
  ManagerLifeId: life,
  TodoWriteId: write,
  TodoReviewId: reviewId,
  DedicatedReviewerId: dedicated,
  ReviewerSessionId: reviewerSession,
  ReviewWorkStartCursor: { Sequence: 4 },
  ManagerReviewFrontier: { Sequence: 10 },
}
const concluded = {
  ManagerLifeId: life,
  TodoWriteId: write,
  TodoReviewId: reviewId,
  DedicatedReviewerId: dedicated,
  ReviewerSessionId: reviewerSession,
  Verdict: 'REVISE',
  WorkRecordRef: 'review-lwr',
  WorkRecordDigest: 'review-lwr-digest',
  SettledTodoRef: 'settled-list',
  SettledTodoDigest: 'settled-list-digest',
  ReviewerRecordFrontier: { Sequence: 8 },
  ProviderRunId: 'reviewer-provider-run',
  ToolCallId: 'reviewer-call',
}

const populated = () => {
  const projection = todo.newProjection()
  for (const [eventId, caseName, payload] of [
    ['prepared-fact-ref', 'TodoWritePrepared', prepared],
    ['accepted-fact-ref', 'TodoWriteAccepted', accepted],
    ['enlisted-fact-ref', 'DedicatedTodoReviewerEnlisted', enlisted],
    ['assigned-fact-ref', 'TodoProcessReviewAssigned', assigned],
  ]) {
    const result = todo.fold(projection, eventId, caseName, payload)
    assert.equal(result.ok, true, JSON.stringify(result))
  }
  return projection
}

const open = async (label) => {
  const directory = mkdtempSync(join(tmpdir(), `wxs-consumable-${label}-`))
  const opened = await journal.JournalSurface_bootWithWriterId(directory, `writer-${label}`, `rt-${label}`, 4242, '2026-01-01T00:00:00Z')
  assert.equal(opened.ok, true, JSON.stringify(opened))
  return { directory, opened, cleanup: () => { journal.JournalSurface_dispose(opened.journal); rmSync(directory, { recursive: true, force: true }) } }
}

const appendReviewFacts = async (opened, reviewerSessionId) => {
  const preparedResult = await todo.appendFact(opened.journal, managerSession, null, 'TodoWritePrepared', prepared)
  assert.equal(preparedResult.ok, true, JSON.stringify(preparedResult))
  const acceptedResult = await todo.appendFact(
    opened.journal,
    managerSession,
    null,
    'TodoWriteAccepted',
    { ...accepted, PreparedFactRef: preparedResult.eventId },
  )
  assert.equal(acceptedResult.ok, true, JSON.stringify(acceptedResult))
  const enlistedResult = await todo.appendFact(
    opened.journal,
    managerSession,
    null,
    'DedicatedTodoReviewerEnlisted',
    { ...enlisted, ReviewerSessionId: reviewerSessionId },
  )
  assert.equal(enlistedResult.ok, true, JSON.stringify(enlistedResult))
  const assignedResult = await todo.appendFact(
    opened.journal,
    managerSession,
    null,
    'TodoProcessReviewAssigned',
    { ...assigned, ReviewerSessionId: reviewerSessionId },
  )
  assert.equal(assignedResult.ok, true, JSON.stringify(assignedResult))
}

test('WHAT[REVIEW-ASSURANCE-011] REVIEW_014_a_process_verdict_is_not_a_consumable_marker', () => {
  const guard = review.emptyGuard()
  const attempt = review.attemptIdentity('bar_process', review.verdictWitness({ ProviderRun: 'run-1', ToolCallId: 'call-1', GitTreeHash: 'tree-1', ReviewerSessionId: reviewerSession }))
  const applied = review.applyVerdict(attempt, 'REVISE', guard)
  assert.equal(applied.ok, true)
  assert.equal(review.guardView(applied.value).witness.state, 'RevisionWitness')
  assert.equal(review.satisfiesGuard('tree-1', applied.value), false)
  assert.equal(todo.view(populated(), life).checkpoints.find((item) => item.todoWriteId === write).concluded, null)
})

test('WHAT[REVIEW-ASSURANCE-008] REVIEW_014_concluded_marker_is_durable_and_consumable', () => {
  const projection = populated()
  const result = todo.fold(projection, 'concluded-fact-ref', 'TodoReviewConcluded', concluded)
  assert.equal(result.ok, true, JSON.stringify(result))
  const checkpoint = todo.view(projection, life).checkpoints.find((item) => item.todoWriteId === write)
  assert.ok(checkpoint.concluded)
  assert.equal(checkpoint.concluded.verdict, 'REVISE')
  assert.equal(checkpoint.concluded.workRecordRef, 'review-lwr')
})

test('WHAT[REVIEW-ASSURANCE-012] REVIEW_016_unknown_verdict_and_physical_evidence_fail_closed', () => {
  const invalidVerdict = todo.factJson('TodoReviewConcluded', { ...concluded, Verdict: 'UNKNOWN' })
  assert.equal(invalidVerdict.ok, false)
  assert.match(invalidVerdict.error, /unknown review verdict/)

  const invalidEvidence = todo.factJson('TodoWriteAccepted', { ...accepted, PhysicalSuccessEvidence: 'UNKNOWN' })
  assert.equal(invalidEvidence.ok, false)
  assert.match(invalidEvidence.error, /unknown physical success evidence/)
})

test('WHAT[REVIEW-ASSURANCE-012] REVIEW_016_concluded_fact_freezes_the_request_frontier', () => {
  const encoded = JSON.parse(todo.factJson('TodoReviewConcluded', concluded))
  assert.equal(String(encoded.ReviewerRecordFrontier.Sequence), '8')
  assert.notEqual(String(encoded.ReviewerRecordFrontier.Sequence), String(prepared.ReviewFrontier.Sequence))
})

test('WHAT[REVIEW-ASSURANCE-010] REVIEW_018_concluded_without_accepted_is_rejected', () => {
  const projection = todo.newProjection()
  assert.deepEqual(todo.fold(projection, 'concluded-only', 'TodoReviewConcluded', concluded), { ok: false, error: { code: 'ConcludedWithoutAccepted', todoWriteId: write } })
})

test('WHAT[REVIEW-ASSURANCE-010] REVIEW_018_concluded_without_assignment_is_rejected', () => {
  const projection = todo.newProjection()
  assert.equal(todo.fold(projection, 'prepared-fact-ref', 'TodoWritePrepared', prepared).ok, true)
  assert.equal(todo.fold(projection, 'accepted-fact-ref', 'TodoWriteAccepted', accepted).ok, true)
  assert.deepEqual(todo.fold(projection, 'concluded', 'TodoReviewConcluded', concluded), { ok: false, error: { code: 'AssignmentWithoutAccepted', todoWriteId: write } })
})

test('WHAT[REVIEW-ASSURANCE-010] REVIEW_018_concluded_binds_to_assignment_identity', () => {
  const projection = populated()
  const foreign = { ...concluded, TodoReviewId: 'review-other' }
  assert.deepEqual(todo.fold(projection, 'concluded', 'TodoReviewConcluded', foreign), { ok: false, error: { code: 'IdentityCorruption', field: 'TodoReviewAssignment' } })
  assert.equal(todo.view(projection, life).checkpoints.find((item) => item.todoWriteId === write).concluded, null)
})

test('WHAT[REVIEW-ASSURANCE-009] REVIEW_013_verdict_requires_closed_reviewer_turn_before_conclusion', async () => {
  const { opened, cleanup } = await open('pending')
  try {
    await appendReviewFacts(opened, reviewerSession)
    await reviewJournal.appendReview(opened.journal, reviewerSession, null, 'ReviewBarrierStarted', { ReviewerSessionId: reviewerSession, ManagerSessionId: managerSession, BarrierId: 'bar-process', GitTreeHash: 'tree-1' })
    assert.equal(todo.processIdleDisposition(opened.journal, reviewerSession), 'OrdinaryRepair')
    await reviewJournal.appendReview(opened.journal, reviewerSession, 'run-1', 'ReviewVerdictRecorded', { ReviewerSessionId: reviewerSession, ManagerSessionId: managerSession, BarrierId: 'bar-process', GitTreeHash: 'tree-1', ProviderRun: 'run-1', ToolCallId: 'call-1', Verdict: 'PERFECT' })
    assert.equal(
      todo.processIdleDisposition(opened.journal, reviewerSession),
      'CompleteToolOnlyProcessReview',
      'a process reviewer that already durably judged must close at stable idle instead of entering missing-final-report repair',
    )
    const pending = await todo.tryConclude(opened.journal, life, write)
    assert.equal(pending.status, 'Pending')
    assert.match(pending.reason, /not closed/i)
  } finally { cleanup() }
})

test('WHAT[REVIEW-ASSURANCE-009] REVIEW_013_stranded_terminal_verdict_recovers_closure_from_durable_tool_result', async () => {
  const { opened, cleanup } = await open('recover-closure')
  try {
    await appendReviewFacts(opened, reviewerSession)
    await reviewJournal.appendReview(opened.journal, reviewerSession, null, 'ReviewBarrierStarted', { ReviewerSessionId: reviewerSession, ManagerSessionId: managerSession, BarrierId: 'bar-process', GitTreeHash: 'tree-1' })
    await reviewJournal.appendReview(opened.journal, reviewerSession, 'run-1', 'ReviewVerdictRecorded', { ReviewerSessionId: reviewerSession, ManagerSessionId: managerSession, BarrierId: 'bar-process', GitTreeHash: 'tree-1', ProviderRun: 'run-1', ToolCallId: 'call-1', Verdict: 'PERFECT' })
    const traced = await reviewJournal.appendAgent(opened.journal, reviewerSession, 'run-1', 'Companion', 'XTracePartAppended', {
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
    })
    assert.equal(traced.ok, true, JSON.stringify(traced))

    await todo.tryConclude(opened.journal, life, write)

    const view = reviewJournal.sessionView(opened.journal, reviewerSession)
    assert.equal(view.closedAttempts.length, 1)
    assert.deepEqual(view.closedAttempts[0], { providerRun: 'run-1', toolCallId: 'call-1', frontier: 6n })
  } finally { cleanup() }
})

test('WHAT[REVIEW-ASSURANCE-009] REVIEW_013_recovery_uses_latest_exact_tool_result_not_current_xtrace_head', async () => {
  const { opened, cleanup } = await open('recover-exact-frontier')
  try {
    await appendReviewFacts(opened, reviewerSession)
    await reviewJournal.appendReview(opened.journal, reviewerSession, null, 'ReviewBarrierStarted', { ReviewerSessionId: reviewerSession, ManagerSessionId: managerSession, BarrierId: 'bar-process', GitTreeHash: 'tree-1' })
    await reviewJournal.appendReview(opened.journal, reviewerSession, 'run-1', 'ReviewVerdictRecorded', { ReviewerSessionId: reviewerSession, ManagerSessionId: managerSession, BarrierId: 'bar-process', GitTreeHash: 'tree-1', ProviderRun: 'run-1', ToolCallId: 'call-1', Verdict: 'PERFECT' })

    const appendPart = (run, call, sequence, kind) => reviewJournal.appendAgent(opened.journal, reviewerSession, run, 'Companion', 'XTracePartAppended', {
      SessionId: reviewerSession,
      CursorSequence: sequence,
      Role: kind === 'user' ? 'user' : 'assistant',
      Turn: sequence,
      PartIndex: 0,
      Kind: kind,
      ToolName: null,
      TextRef: `blob-${sequence}`,
      TextDigest: `digest-${sequence}`,
      Provenance: `g:0/msg:${run}/host-part:${sequence}`,
      ProviderRun: kind === 'user' ? null : run,
      ToolCallId: kind === 'tool_result' ? call : null,
      HostToolPartId: `prt-${sequence}`,
    })

    assert.equal((await appendPart('run-1', 'call-1', 5, 'tool_result')).ok, true)
    assert.equal((await appendPart('retry-root', null, 6, 'user')).ok, true)
    await reviewJournal.appendReview(opened.journal, reviewerSession, 'run-2', 'ReviewVerdictRecorded', { ReviewerSessionId: reviewerSession, ManagerSessionId: managerSession, BarrierId: 'bar-process', GitTreeHash: 'tree-1', ProviderRun: 'run-2', ToolCallId: 'call-2', Verdict: 'PERFECT' })
    assert.equal((await appendPart('run-2', 'call-2', 9, 'tool_result')).ok, true)
    assert.equal((await appendPart('late-run', null, 10, 'reasoning')).ok, true)

    await todo.tryConclude(opened.journal, life, write)

    const view = reviewJournal.sessionView(opened.journal, reviewerSession)
    assert.equal(view.closedAttempts.length, 1)
    assert.deepEqual(view.closedAttempts[0], { providerRun: 'run-2', toolCallId: 'call-2', frontier: 10n })
    assert.equal(view.xTraceHead, 11n, 'later durable history must not widen the recovered review frontier')
  } finally { cleanup() }
})

test('WHAT[REVIEW-ASSURANCE-010] REVIEW_018_absent_reviewer_fails_closed_without_fabricated_conclusion', async () => {
  const { opened, cleanup } = await open('absent')
  try {
    await appendReviewFacts(opened, 'ses-ghost')
    const presence = todo.producerPresence(opened.journal, life, write)
    assert.equal(presence.status, 'Absent')
    assert.match(presence.reason, /reviewer session missing/)
    const waited = await todo.awaitConsumableReview(opened.journal, life, write)
    assert.equal(waited.ok, false)
    assert.match(waited.error, /process review cannot progress: reviewer session missing/)
  } finally { cleanup() }
})
