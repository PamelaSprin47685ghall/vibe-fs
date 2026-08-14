// requirements/review-assurance/tests/consumable-review.test.mjs
//
// REVIEW-014/017/018/020 (TODO-006, GLORY-072/073 crossing): the two-stage
// consumability contract. A durable reviewer verdict (VerdictKnown) settles the
// checkpoint business outcome, but ONLY `TodoReviewConcluded` — appended after a
// record-ready LWR in the same snapshot — makes the review consumable by the
// next TodoWrite / suicide drain. The projection must never fabricate
// consumability from a verdict, must bind the conclusion to its assignment, and
// must fail closed when the process-review producer is absent (infra failure is
// never a REVISE and never a premature Concluded).

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import { HandleCompletionKind, HandleOwnership } from '../../../dist/Kernel/Fact.js'
import {
  agentFact,
  agentJournal,
  authorityRoot,
  blobDigest,
  blobRef,
  caseOf,
  envelope,
  eventId,
  fact,
  fold,
  gitTreeHash,
  handleId,
  idValue,
  logicalRunId,
  magicTodo,
  magicTodoJournal,
  managerLifeId,
  mapEntries,
  providerRun,
  resultOf,
  reviewBarrierId,
  reviewProjection,
  reviewWitness,
  roles,
  sessionId,
  stream,
  toolCallId,
  verdict,
  verdictWitness,
} from '../../verification-system/tests/support/domain.mjs'

const { tryConclude, producerPresence, awaitConsumableReview } = await import(
  '../../../dist/Application/Review/TodoProcessReviewProgram.js'
)

const sha256 = (value) => `digest:${value}`
const life = managerLifeId('life-consumable')
const managerSession = sessionId('ses-manager')
const reviewerSession = sessionId('ses-rev')
const call = toolCallId('todo-call')
const cursor = (sequence) => new magicTodoJournal.XTraceCursor(BigInt(sequence))

const write = magicTodo.todoWriteId(sha256, life, call)
const review = magicTodo.todoReviewId(sha256, life, write)
const reviewer = magicTodo.dedicatedReviewerId(sha256, life)

const prepared = new magicTodoJournal.TodoWritePrepared(
  managerSession,
  life,
  write,
  call,
  2,
  blobRef('base-list'),
  blobDigest('base-digest'),
  blobRef('proposal-list'),
  blobDigest('proposal-digest'),
  true,
  'provider-input-digest',
  cursor(10),
  'magic-v1',
)
const accepted = new magicTodoJournal.TodoWriteAccepted(
  life,
  write,
  call,
  eventId('prepared-fact-ref'),
  'provider-input-digest',
  'output-digest',
  magicTodoJournal.PhysicalSuccessEvidence.LiveAfterSuccess,
  'magic-v1',
)
const enlisted = new magicTodoJournal.DedicatedTodoReviewerEnlisted(life, reviewer, reviewerSession)
const assigned = new magicTodoJournal.TodoProcessReviewAssigned(life, write, review, reviewer, reviewerSession, cursor(4), cursor(10))
const concluded = new magicTodoJournal.TodoReviewConcluded(
  life,
  write,
  review,
  reviewer,
  reviewerSession,
  magicTodo.revise,
  blobRef('review-lwr'),
  blobDigest('review-lwr-digest'),
  blobRef('settled-list'),
  blobDigest('settled-list-digest'),
  cursor(8),
  providerRun('reviewer-provider-run'),
  toolCallId('reviewer-call'),
)

const magicFact = (caseName, payload) => magicTodoJournal.MagicTodoFact(caseName, [payload])
const foldMagic = (state, magicFactValue, envelopeEventId = eventId(`evt-${Math.random().toString(36).slice(2)}`)) =>
  resultOf(magicTodoJournal.fold(envelopeEventId, state, magicFactValue))

/** Accepted + enlisted + assigned, in order. */
const checkpointState = () => {
  let state = magicTodoJournal.empty
  state = foldMagic(state, magicFact('TodoWritePrepared', prepared), eventId('prepared-fact-ref')).value
  state = foldMagic(state, magicFact('TodoWriteAccepted', accepted)).value
  state = foldMagic(state, magicFact('DedicatedTodoReviewerEnlisted', enlisted)).value
  state = foldMagic(state, magicFact('TodoProcessReviewAssigned', assigned)).value
  return state
}

const checkpoint = (state) => {
  const lifeState = state.ByLife.get(idValue.managerLife(life))
  return mapEntries(lifeState.Checkpoints).find(([key]) => key === magicTodo.todoWriteIdValue(write))[1]
}

// ── REVIEW-014: VerdictKnown vs ConsumableReview ────────────────────────────

test('REVIEW_014_a_durable_verdict_alone_never_makes_the_review_consumable', () => {
  const state = checkpointState()
  const before = checkpoint(state)
  assert.equal(before.Accepted, true)
  assert.equal(before.Assignment == null, false, 'Rk must be assigned')
  assert.equal(before.Concluded == null, true, 'no Concluded fact yet')

  // VerdictKnown: a durable reviewer verdict lands in the Reviewer domain
  // guard. It settles the checkpoint business outcome (REVIEW-011/013), but it
  // is not a consumable report.
  const barrier = reviewBarrierId('bar_process')
  const attempt = reviewWitness.attemptIdentity(
    barrier,
    verdictWitness({ run: 'run_1', call: 'call_1', tree: 'tree_1', reviewer: 'ses-rev' }),
  )
  const revised = reviewProjection.applyVerdict(attempt, verdict.revise, reviewProjection.empty)
  assert.equal(revised.ok, true)
  assert.equal(caseOf(revised.value.Witness), 'RevisionWitness', 'VerdictKnown exists in the Reviewer domain')

  // The Magic Todo projection is untouched by the verdict: still no Concluded.
  // REVIEW-014 forbids squeezing "only a verdict, no report" into
  // TodoReviewConcluded; the checkpoint marker stays null.
  const after = checkpoint(state)
  assert.equal(after.Concluded == null, true, 'verdict must not fabricate consumability')
})

test('REVIEW_014_only_todo_review_concluded_marks_the_review_consumable', () => {
  let state = checkpointState()
  state = foldMagic(state, magicFact('TodoReviewConcluded', concluded)).value

  const cp = checkpoint(state)
  assert.equal(cp.Concluded == null, false, 'TodoReviewConcluded sets the consumable marker')
  assert.equal(cp.Concluded.Verdict, magicTodo.revise)
  assert.equal(cp.Concluded.WorkRecordRef.fields[0], blobRef('review-lwr').fields[0])
  // The record identity carries the frozen reviewer frontier (REVIEW-016):
  // evidence is bounded to the request, not the session head.
  assert.equal(cp.Concluded.ReviewerRecordFrontier.Sequence, 8n)
})

// ── REVIEW-018: the projection cannot fabricate consumability ────────────────

test('REVIEW_018_concluded_without_accepted_is_rejected', () => {
  let state = magicTodoJournal.empty
  state = foldMagic(state, magicFact('TodoWritePrepared', prepared), eventId('prepared-fact-ref')).value

  const rejected = foldMagic(state, magicFact('TodoReviewConcluded', concluded))
  assert.equal(rejected.ok, false)
  assert.equal(caseOf(rejected.error), 'ConcludedWithoutAccepted')
})

test('REVIEW_018_concluded_without_assignment_is_rejected', () => {
  let state = magicTodoJournal.empty
  state = foldMagic(state, magicFact('TodoWritePrepared', prepared), eventId('prepared-fact-ref')).value
  state = foldMagic(state, magicFact('TodoWriteAccepted', accepted)).value

  const rejected = foldMagic(state, magicFact('TodoReviewConcluded', concluded))
  assert.equal(rejected.ok, false)
  assert.equal(caseOf(rejected.error), 'AssignmentWithoutAccepted')
})

test('REVIEW_018_concluded_must_bind_to_its_assignment_identity', () => {
  // REVIEW-006/051: evidence must bind to the reviewed object. A conclusion
  // that names a different review/todo/reviewer than the assignment is a fold
  // rejection — the record cannot be attached to the wrong request.
  const foreignReview = magicTodo.todoReviewId(sha256, life, toolCallId('call-other'))
  const mismatched = new magicTodoJournal.TodoReviewConcluded(
    life,
    write,
    foreignReview,
    reviewer,
    reviewerSession,
    magicTodo.revise,
    blobRef('review-lwr'),
    blobDigest('review-lwr-digest'),
    blobRef('settled-list'),
    blobDigest('settled-list-digest'),
    cursor(8),
    providerRun('reviewer-provider-run'),
    toolCallId('reviewer-call'),
  )

  let state = checkpointState()
  const rejected = foldMagic(state, magicFact('TodoReviewConcluded', mismatched))
  assert.equal(rejected.ok, false)
  assert.equal(caseOf(rejected.error), 'IdentityCorruption')
  assert.equal(checkpoint(state).Concluded == null, true, 'the checkpoint stays unconsumable after a rejected conclusion')
})

// ── REVIEW-020: a process verdict never enters the terminal witness algebra ──

test('REVIEW_020_a_process_revise_is_a_revision_witness_not_a_finality_rejection', () => {
  // Fold a process REVISE through the reviewer session projection: the guard
  // becomes RevisionWitness. No ConfirmedReviewWitness fact is produced, no
  // dual-PERFECT algebra is entered, and the Magic Todo checkpoint is not
  // Concluded by it (REVIEW-014 separation, above).
  const barrier = reviewBarrierId('bar_process')
  const session = sessionId('ses-rev')

  const opened = fold.one(
    fold.empty,
    envelope({
      stream: stream.session(session),
      fact: fact('ReviewBarrierStarted', {
        ReviewerSessionId: session,
        ManagerSessionId: managerSession,
        BarrierId: barrier,
        GitTreeHash: gitTreeHash('tree_1'),
      }),
    }),
  )
  assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
  const guard = fold.sessions(opened.value)['ses-rev'].ReviewGuard
  assert.equal(caseOf(guard.Witness), 'NoReview')

  const verdictRecorded = fold.one(
    opened.value,
    envelope({
      stream: stream.session(session),
      fact: fact('ReviewVerdictRecorded', {
        ReviewerSessionId: session,
        ManagerSessionId: managerSession,
        BarrierId: barrier,
        GitTreeHash: gitTreeHash('tree_1'),
        ProviderRun: providerRun('run_1'),
        ToolCallId: toolCallId('call_1'),
        Verdict: verdict.revise,
      }),
    }),
  )
  assert.equal(verdictRecorded.ok, true, verdictRecorded.ok ? '' : JSON.stringify(verdictRecorded.error))
  assert.equal(caseOf(fold.sessions(verdictRecorded.value)['ses-rev'].ReviewGuard.Witness), 'RevisionWitness')
  assert.equal(
    reviewProjection.satisfiesGuard(gitTreeHash('tree_1'), fold.sessions(verdictRecorded.value)['ses-rev'].ReviewGuard),
    false,
  )

  // The reviewer guard is the only place the process verdict lives: the Magic
  // Todo projection still shows an unconsumed, unconcluded checkpoint.
  assert.equal(checkpoint(checkpointState()).Concluded == null, true)
})

// ── REVIEW-017/018: record-ready wait is event-driven and fails closed ───────

test('REVIEW_018_await_consumable_review_fails_closed_when_the_producer_is_absent', async () => {
  // A checkpoint whose reviewer session never materialised must not hang the
  // waiter nor fabricate a Concluded: this is the infra-failure fail-closed
  // path (REVIEW-018), not a semantic REVISE.
  const directory = mkdtempSync(join(tmpdir(), 'wxs-consumable-'))
  const created = await agentJournal.create({ directory, runtime: 'rt_consumable' })
  assert.equal(created.ok, true, created.ok ? '' : String(created.error))

  try {
    const journal = created.journal

    const ghost = sessionId('ses-ghost')
    const ghostAssigned = new magicTodoJournal.TodoProcessReviewAssigned(
      life,
      write,
      review,
      reviewer,
      ghost,
      cursor(4),
      cursor(10),
    )

    const append = async (caseName, payload) => {
      const appended = await agentJournal.appendMagicTodo(
        stream.session(managerSession),
        undefined,
        magicFact(caseName, payload),
        journal,
      )
      assert.equal(appended.ok, true, appended.ok ? '' : String(appended.error))
      // MagicTodoAppendReceipt { EventId; Projection } → the named field is the EventId.
      return appended.value.EventId
    }

    // TodoWriteAccepted must name the exact Prepared envelope (TODO-004), so
    // the ref comes from the append receipt — never invented.
    const preparedRef = await append('TodoWritePrepared', prepared)
    const acceptedWithRef = new magicTodoJournal.TodoWriteAccepted(
      life,
      write,
      call,
      preparedRef,
      'provider-input-digest',
      'output-digest',
      magicTodoJournal.PhysicalSuccessEvidence.LiveAfterSuccess,
      'magic-v1',
    )
    await append('TodoWriteAccepted', acceptedWithRef)
    await append('DedicatedTodoReviewerEnlisted', new magicTodoJournal.DedicatedTodoReviewerEnlisted(life, reviewer, ghost))
    await append('TodoProcessReviewAssigned', ghostAssigned)

    // No verdict in the ghost guard → pending, never concluded.
    const conclude = await tryConclude(journal, life, write)
    assert.equal(caseOf(conclude), 'Pending')

    // The producer is absent (no reviewer session exists) → fail closed, no
    // timer, no polling, no fabricated Concluded.
    const presence = await producerPresence(journal, life, write)
    assert.equal(caseOf(presence), 'Absent')

    const waited = resultOf(await awaitConsumableReview(journal, life, write))
    assert.equal(waited.ok, false)
    assert.match(waited.error, /process review cannot progress: reviewer session missing/)

    const snap = agentJournal.snapshot(journal)
    const lifeState = snap.AgentProjections.MagicTodo.ByLife.get(idValue.managerLife(life))
    const cp = mapEntries(lifeState.Checkpoints).find(([key]) => key === magicTodo.todoWriteIdValue(write))[1]
    assert.equal(cp.Concluded == null, true, 'fail-closed wait must not fabricate a Concluded')
  } finally {
    created.dispose()
    rmSync(directory, { recursive: true, force: true })
  }
})

test('REVIEW_018_producer_presence_is_present_when_reviewer_handle_is_CompletedAwaitingJoin', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-completed-presence-'))
  const created = await agentJournal.create({ directory, runtime: 'rt_completed_presence' })
  assert.equal(created.ok, true, created.ok ? '' : String(created.error))

  try {
    const journal = created.journal
    const reviewerSession = sessionId('ses-completed-reviewer')
    const assigned = new magicTodoJournal.TodoProcessReviewAssigned(
      life,
      write,
      review,
      reviewer,
      reviewerSession,
      cursor(4),
      cursor(10),
    )

    const append = async (caseName, payload) => {
      const appended = await agentJournal.appendMagicTodo(
        stream.session(managerSession),
        undefined,
        magicFact(caseName, payload),
        journal,
      )
      assert.equal(appended.ok, true, appended.ok ? '' : String(appended.error))
      return appended.value.EventId
    }

    const preparedRef = await append('TodoWritePrepared', prepared)
    const acceptedWithRef = new magicTodoJournal.TodoWriteAccepted(
      life,
      write,
      call,
      preparedRef,
      'provider-input-digest',
      'output-digest',
      magicTodoJournal.PhysicalSuccessEvidence.LiveAfterSuccess,
      'magic-v1',
    )
    await append('TodoWriteAccepted', acceptedWithRef)
    await append('DedicatedTodoReviewerEnlisted', new magicTodoJournal.DedicatedTodoReviewerEnlisted(life, reviewer, reviewerSession))
    await append('TodoProcessReviewAssigned', assigned)

    // Simulate child session creation + completion
    await agentJournal.appendAgent(
      stream.session(reviewerSession),
      undefined,
      agentFact('AuthorityRootAccepted', {
        SessionId: reviewerSession,
        LogicalRunId: logicalRunId('run-rev'),
        AuthorityRootUserMessageId: authorityRoot('root-rev'),
        AuthorityKind: 'ChildRoot',
        SelectedAgent: 'fast-reviewer',
        PeerAgent: 'deep-reviewer',
        CanonicalRole: 'Reviewer',
        SelectedTier: 'fast',
      }),
      journal,
    )
    await agentJournal.appendAgent(
      stream.session(managerSession),
      undefined,
      agentFact('HandleLinked', {
        ParentSessionId: managerSession,
        Handle: handleId.agent('h_rev'),
        TargetAgent: 'fast-reviewer',
        ChildSessionId: reviewerSession,
        CanonicalRole: roles.of('Reviewer'),
        Ownership: HandleOwnership.HostOwnedHidden,
      }),
      journal,
    )
    await agentJournal.appendAgent(
      stream.session(managerSession),
      undefined,
      agentFact('HandleCompleted', {
        ParentSessionId: managerSession,
        Handle: handleId.agent('h_rev'),
        ChildSessionId: reviewerSession,
        CompletionKind: HandleCompletionKind.Terminal,
        CompletionRef: undefined,
        CompletionDigest: undefined,
      }),
      journal,
    )

    const presence = await producerPresence(journal, life, write)
    assert.equal(caseOf(presence), 'Present', 'CompletedAwaitingJoin handle must be Present, not Absent')
  } finally {
    created.dispose()
    rmSync(directory, { recursive: true, force: true })
  }
})

test('REVIEW_017 durable verdict keeps record-ready producer present after the reviewer work-unit is Retired', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-retired-verdict-presence-'))
  const created = await agentJournal.create({ directory, runtime: 'rt_retired_verdict_presence' })
  assert.equal(created.ok, true, created.ok ? '' : String(created.error))

  try {
    const journal = created.journal
    const retiredReviewerSession = sessionId('ses-retired-verdict-reviewer')
    const barrier = reviewBarrierId(review.fields[0])
    const retiredAssigned = new magicTodoJournal.TodoProcessReviewAssigned(
      life,
      write,
      review,
      reviewer,
      retiredReviewerSession,
      cursor(4),
      cursor(10),
    )

    const append = async (caseName, payload) => {
      const appended = await agentJournal.appendMagicTodo(
        stream.session(managerSession),
        undefined,
        magicFact(caseName, payload),
        journal,
      )
      assert.equal(appended.ok, true, appended.ok ? '' : String(appended.error))
      return appended.value.EventId
    }
    const appendAgent = async (session, payload) => {
      const appended = await agentJournal.appendAgent(stream.session(session), undefined, payload, journal)
      assert.equal(appended.ok, true, appended.ok ? '' : String(appended.error))
    }

    const preparedRef = await append('TodoWritePrepared', prepared)
    await append('TodoWriteAccepted', new magicTodoJournal.TodoWriteAccepted(
      life,
      write,
      call,
      preparedRef,
      'provider-input-digest',
      'output-digest',
      magicTodoJournal.PhysicalSuccessEvidence.LiveAfterSuccess,
      'magic-v1',
    ))
    await append('DedicatedTodoReviewerEnlisted', new magicTodoJournal.DedicatedTodoReviewerEnlisted(life, reviewer, retiredReviewerSession))
    await append('TodoProcessReviewAssigned', retiredAssigned)

    await appendAgent(retiredReviewerSession, agentFact('AuthorityRootAccepted', {
      SessionId: retiredReviewerSession,
      LogicalRunId: logicalRunId('run-retired-reviewer'),
      AuthorityRootUserMessageId: authorityRoot('root-retired-reviewer'),
      AuthorityKind: 'ChildRoot',
      SelectedAgent: 'fast-reviewer',
      PeerAgent: 'deep-reviewer',
      CanonicalRole: 'Reviewer',
      SelectedTier: 'fast',
    }))
    await appendAgent(managerSession, agentFact('HandleLinked', {
      ParentSessionId: managerSession,
      Handle: handleId.agent('h_retired_verdict'),
      TargetAgent: 'fast-reviewer',
      ChildSessionId: retiredReviewerSession,
      CanonicalRole: roles.of('Reviewer'),
      Ownership: HandleOwnership.HostOwnedHidden,
    }))
    await appendAgent(retiredReviewerSession, agentFact('ReviewBarrierStarted', {
      ReviewerSessionId: retiredReviewerSession,
      ManagerSessionId: managerSession,
      BarrierId: barrier,
      GitTreeHash: gitTreeHash('tree-retired-verdict'),
    }))
    await appendAgent(retiredReviewerSession, agentFact('ReviewVerdictRecorded', {
      ReviewerSessionId: retiredReviewerSession,
      ManagerSessionId: managerSession,
      BarrierId: barrier,
      GitTreeHash: gitTreeHash('tree-retired-verdict'),
      ProviderRun: providerRun('run-retired-verdict'),
      ToolCallId: toolCallId('judge-retired-verdict'),
      Verdict: verdict.perfect,
    }))
    await appendAgent(managerSession, agentFact('HandleCompleted', {
      ParentSessionId: managerSession,
      Handle: handleId.agent('h_retired_verdict'),
      ChildSessionId: retiredReviewerSession,
      CompletionKind: HandleCompletionKind.Terminal,
      CompletionRef: undefined,
      CompletionDigest: undefined,
    }))
    await appendAgent(managerSession, agentFact('HandleRetired', {
      ParentSessionId: managerSession,
      Handle: handleId.agent('h_retired_verdict'),
    }))

    const conclude = await tryConclude(journal, life, write)
    assert.equal(caseOf(conclude), 'Pending', 'verdict exists but LWR is intentionally not record-ready')
    const presence = await producerPresence(journal, life, write)
    assert.equal(caseOf(presence), 'Present', 'Retired after a durable verdict must keep waiting for Journal/XTrace record-ready convergence')
  } finally {
    created.dispose()
    rmSync(directory, { recursive: true, force: true })
  }
})
