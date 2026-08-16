// requirements/review-judgement/tests/process-review-judgement.test.mjs
//
// REVIEW-JUDGEMENT-008 (REVIEW-013 process part): a TodoProcessReview verdict is
// a genuine judgement of the checkpoint work, and it is terminal after ONE
// durable judge — no challenge, no dual-PERFECT, no confirmation nudge. The
// cadence (1:1 lag-1 Rk) is obligation-ledger's; here we pin the judgement-side
// semantics: the typed RequestKind split, the one-judgement preamble, the
// assignment surface that stays free of confirmation machinery, and the guard
// that never turns a bare process verdict into a Confirmed witness.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  caseOf,
  gitTreeHash,
  magicTodo,
  managerLifeId,
  providerLanguage,
  providerResources,
  reviewBarrierId,
  reviewProjection,
  reviewWitness,
  toList,
  toolCallId,
  verdict,
  verdictWitness,
} from '../../verification-system/tests/support/domain.mjs'

const { needsEnsureReview, renderAssignmentUserMessage, ReviewRequestKind } = await import(
  '../../../dist/Mission/Obligation/Todo/ProcessReview.js'
)
const { renderObligationListWire } = await import('../../../dist/Mission/Obligation/Todo/Surface.js')

const sha256 = (value) => `digest:${value}`
const life = managerLifeId('life-process-judgement')
const write = magicTodo.todoWriteId(sha256, life, toolCallId('call-t1'))
const review = magicTodo.todoReviewId(sha256, life, write)

test('WHAT[REVIEW-JUDGEMENT-008] REVIEW_013_the_request_kind_is_typed_process_vs_finality', () => {
  // REVIEW-013 forbids guessing process vs Finality from `pendingChallenge`;
  // the assignment authority carries a typed kind instead.
  assert.equal(caseOf(ReviewRequestKind.TodoProcess), 'TodoProcess')
  assert.equal(caseOf(ReviewRequestKind.FinalityTerminal), 'FinalityTerminal')
})

test('WHAT[REVIEW-JUDGEMENT-008] REVIEW_013_ensure_review_stays_outstanding_until_concluded', () => {
  // Accepted ∧ ¬Concluded → Rk pending from any reentry site. Once Concluded
  // exists, no further ensureReview is needed — the verdict is terminal.
  assert.equal(needsEnsureReview(true, false), true)
  assert.equal(needsEnsureReview(false, false), false)
  assert.equal(needsEnsureReview(true, true), false)
  assert.equal(needsEnsureReview(false, true), false)
})

test('WHAT[REVIEW-JUDGEMENT-008] REVIEW_013_process_preamble_commands_exactly_one_verdict_and_disclaims_terminal_witness', () => {
  const preamble = providerResources.readText(providerLanguage.english, 'lifecycle/magic-todo/process-reviewer-preamble')

  // One judgement, one tool call. Process PERFECT is explicitly NOT a terminal
  // Finality witness (REVIEW-020 / GLORY-058 boundary, asserted from the
  // judgement side here; the counting algebra lives in review-assurance).
  assert.match(preamble, /Reply with exactly one judge tool call: PERFECT or REVISE\./)
  assert.doesNotMatch(preamble, /verdict tool/, 'process review must name the real judge tool, never the removed verdict tool')
  assert.match(preamble, /Process PERFECT is not a terminal Finality witness\./)

  // The process surface must not leak the Finality confirmation machinery.
  for (const forbidden of ['challenge', 'Challenge', 'seal', 'Seal', 'dual', '2N', 'barrier', 'Barrier']) {
    assert.equal(preamble.includes(forbidden), false, `process preamble must not mention '${forbidden}'`)
  }
})

test('WHAT[REVIEW-JUDGEMENT-008] REVIEW_013_process_assignment_is_request_range_bounded_without_confirmation_vocabulary', () => {
  const preamble = providerResources.readText(providerLanguage.english, 'lifecycle/magic-todo/process-reviewer-preamble')
  const request = {
    TodoReviewId: review,
    TodoWriteId: write,
    ManagerLifeId: life,
    OpeningRaw: 'original task authority text',
    ManagerCheckpointLwr: 'frontier-bounded work record for this checkpoint',
    OldTodo: toList([]),
    ProposedTodo: toList([]),
  }

  const message = renderAssignmentUserMessage(preamble, request)

  // The bounded inputs are the ones the process judgement consumes (REVIEW-016
  // crossing: the LWR representation itself is work-record's).
  for (const header of [
    '=== OpeningRaw (task authority) ===',
    '=== ManagerCheckpointLWR (includeOpening=false; frontier-bounded) ===',
    '=== PRIOR CURRENT OBLIGATIONS ===',
    '=== ACCEPTED OBLIGATION ACCOUNT UNDER REVIEW ===',
  ]) {
    assert.ok(message.includes(header), `assignment must carry header: ${header}`)
  }

  // One verdict per checkpoint: no challenge / seal / dual-PERFECT vocabulary.
  for (const forbidden of ['challenge', 'Challenge', 'seal', 'Seal', 'dual', '2N', 'barrier', 'Barrier', 'Confirmation']) {
    assert.equal(message.includes(forbidden), false, `process assignment must not mention '${forbidden}'`)
  }

  // The obligations ride the canonical wire renderer (no second DTO surface).
  assert.equal(renderObligationListWire(toList([])), '[]')
})

test('WHAT[REVIEW-JUDGEMENT-008] REVIEW_013_a_process_verdict_never_becomes_a_confirmed_witness_by_itself', () => {
  // One durable judge is terminal for the checkpoint: recording a PERFECT
  // verdict in the reviewer guard counts the attempt but yields NoReview —
  // confirmation requires the full challenge+seal chain (review-assurance),
  // which the process path never enters. REVIEW-JUDGEMENT-008: no second
  // confirmation is required of a process review.
  const barrier = reviewBarrierId('bar_process')
  const tree = gitTreeHash('tree_process')
  const attempt = reviewWitness.attemptIdentity(
    barrier,
    verdictWitness({ run: 'run_1', call: 'call_1', tree: 'tree_process', reviewer: 'ses_process_rev' }),
  )

  const applied = reviewProjection.applyVerdict(attempt, verdict.perfect, reviewProjection.empty)
  assert.equal(applied.ok, true, applied.ok ? '' : applied.error)

  const guard = reviewProjection.read(applied.value)
  assert.equal(guard.observedAttempts, 1)
  assert.equal(guard.witness, 'NoReview', 'a bare process verdict must not fabricate a Confirmed witness')
  assert.equal(reviewProjection.satisfiesGuard(tree, applied.value), false)
  assert.equal(reviewWitness.isConfirmed(applied.value.Witness), false)

  // And the same holds for REVISE: it is a terminal RevisionWitness for the
  // checkpoint, not a Finality rejection fact.
  const revised = reviewProjection.applyVerdict(attempt, verdict.revise, reviewProjection.empty)
  assert.equal(revised.ok, true)
  assert.equal(caseOf(revised.value.Witness), 'RevisionWitness')
  assert.equal(reviewWitness.isConfirmed(revised.value.Witness), false)
})
