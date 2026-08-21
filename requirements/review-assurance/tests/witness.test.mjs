// Finality review evidence laws after the direct-CE clean break.
// A completed witness contains typed identities only. First PERFECT is never persisted as a program position.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as review from '../../../dist/Mission/Review/Assurance/Surface.js'

const BARRIER = 'bar_1'
const TREE = 'tree_1'
const OTHER_TREE = 'tree_2'
const REVIEWER = 'ses_rev'
const FIRST = review.verdictWitness({ run: 'run_1', call: 'call_1', tree: 'tree_1', reviewer: REVIEWER })
const SECOND = review.verdictWitness({ run: 'run_2', call: 'call_2', tree: 'tree_1', reviewer: REVIEWER })
const REVIEW_PHYSICAL = 'msg_review'

const attempt = ({ run = 'run_1', call = 'call_1', tree = 'tree_1' } = {}) =>
  review.attemptIdentity(BARRIER, review.verdictWitness({ run, call, tree, reviewer: REVIEWER }))

const opened = () => review.startBarrier('ses_mgr', BARRIER, TREE, review.emptyGuard())

const confirmed = ({ firstPhysical = REVIEW_PHYSICAL, secondPhysical = REVIEW_PHYSICAL, first = FIRST, second = SECOND } = {}) =>
  review.confirmWitness(BARRIER, firstPhysical, secondPhysical, first, second)

test('WHAT[REVIEW-ASSURANCE-002] REVIEW_003_challenge_text_is_presentation_only_and_localized', () => {
  assert.equal(review.challengePath, 'review/challenge')
  assert.match(review.challengeText('English'), /re-evaluate/i)
  assert.equal(review.challengePrompt('重新检查'), '# 重新检查\n')
})

test('WHAT[REVIEW-ASSURANCE-003] REVIEW_004_attempt_identity_names_all_five_components', () => {
  assert.equal(
    review.dedupeKey(attempt()),
    ['bar_1', 'tree_1', REVIEWER, 'run_1', 'call_1'].join('\u001f'),
  )
})

test('WHAT[REVIEW-ASSURANCE-001] REVIEW_003_two_attempts_require_distinct_run_and_call', () => {
  assert.equal(review.isDistinctAttempt(BARRIER, FIRST, SECOND), true)
  assert.equal(
    review.isDistinctAttempt(
      BARRIER,
      FIRST,
      review.verdictWitness({ run: 'run_1', call: 'call_2', tree: 'tree_1', reviewer: REVIEWER }),
    ),
    false,
  )
  assert.equal(
    review.isDistinctAttempt(
      BARRIER,
      FIRST,
      review.verdictWitness({ run: 'run_2', call: 'call_1', tree: 'tree_1', reviewer: REVIEWER }),
    ),
    false,
  )
})

test('WHAT[REVIEW-ASSURANCE-002] REVIEW_005_single_PERFECT_is_not_a_durable_pending_witness', () => {
  const result = review.applyVerdict(attempt(), 'PERFECT', opened())
  assert.equal(result.ok, true)
  assert.equal(review.guardView(result.value).witness.state, 'NoReview')
  assert.equal(review.guardView(result.value).observedAttempts, 1)
})

test('WHAT[REVIEW-ASSURANCE-010] REVIEW_002_REVISE_is_a_completed_revision_fact', () => {
  const result = review.applyVerdict(attempt(), 'REVISE', opened())
  assert.equal(result.ok, true)
  assert.equal(review.guardView(result.value).witness.state, 'RevisionWitness')
  assert.equal(review.isRevision(result.value), true)
})

test('WHAT[REVIEW-ASSURANCE-003] REVIEW_004_duplicate_attempt_is_refused', () => {
  const first = review.applyVerdict(attempt(), 'PERFECT', opened())
  assert.equal(first.ok, true)
  const duplicate = review.applyVerdict(attempt(), 'PERFECT', first.value)
  assert.equal(duplicate.ok, false)
  assert.equal(duplicate.error, 'DuplicateAttempt')
})

test('WHAT[REVIEW-ASSURANCE-002] REVIEW_003_completed_witness_carries_same_prompt_or_typed_nudge_physical_identity', () => {
  assert.notEqual(confirmed(), null)
  const nudged = confirmed({ secondPhysical: 'msg_nudge' })
  assert.notEqual(nudged, null)
  assert.equal(review.readWitness(nudged).firstPhysical, REVIEW_PHYSICAL)
  assert.equal(review.readWitness(nudged).secondPhysical, 'msg_nudge')
})

test('WHAT[REVIEW-ASSURANCE-001] REVIEW_003_confirmation_still_requires_distinct_attempts', () => {
  const sameRun = review.verdictWitness({ run: 'run_1', call: 'call_2', tree: 'tree_1', reviewer: REVIEWER })
  const sameCall = review.verdictWitness({ run: 'run_2', call: 'call_1', tree: 'tree_1', reviewer: REVIEWER })
  assert.equal(confirmed({ second: sameRun }), null)
  assert.equal(confirmed({ second: sameCall }), null)
})

test('WHAT[REVIEW-ASSURANCE-005] REVIEW_006_confirmed_witness_is_self_contained_typed_evidence', () => {
  const value = confirmed()
  const read = review.readWitness(value)
  assert.deepEqual(read, {
    state: 'Confirmed',
    barrier: 'bar_1',
    tree: 'tree_1',
    first: { run: 'run_1', call: 'call_1', tree: 'tree_1', reviewer: REVIEWER },
    second: { run: 'run_2', call: 'call_2', tree: 'tree_1', reviewer: REVIEWER },
    firstPhysical: REVIEW_PHYSICAL,
    secondPhysical: REVIEW_PHYSICAL,
  })
  assert.equal('authorityRoot' in read, false)
  assert.equal('challengeResultDigest' in read, false)
  assert.equal('secondProviderInputDigest' in read, false)
})

test('WHAT[REVIEW-ASSURANCE-004] REVIEW_005_confirmedReviewer_is_derived_from_witness', () => {
  assert.equal(review.confirmedReviewer(confirmed()), REVIEWER)
  assert.equal(review.confirmedReviewer(review.noReview), null)
})

test('WHAT[REVIEW-ASSURANCE-006] REVIEW_008_tree_change_invalidates_completed_witness', () => {
  assert.equal(review.isValidForTree(TREE, confirmed()), true)
  assert.equal(review.isValidForTree(OTHER_TREE, confirmed()), false)
})

test('WHAT[REVIEW-ASSURANCE-006] REVIEW_008_new_barrier_requires_a_fresh_completed_CE', () => {
  const applied = review.applyConfirmedWitness(
    BARRIER,
    REVIEW_PHYSICAL,
    REVIEW_PHYSICAL,
    FIRST,
    SECOND,
    opened(),
  )
  assert.equal(applied.ok, true)
  assert.equal(review.satisfiesGuard(TREE, applied.value), true)

  const next = review.startBarrier('ses_mgr', 'bar_2', TREE, applied.value)
  assert.equal(review.satisfiesGuard(TREE, next), false)
  assert.equal(review.isConfirmed(next), true, 'completed history remains auditable')
})

test('WHAT[REVIEW-ASSURANCE-006] REVIEW_008_late_old_confirmation_cannot_satisfy_current_barrier', () => {
  const current = review.startBarrier('ses_mgr', 'bar_2', TREE, opened())
  const late = review.applyConfirmedWitness(
    BARRIER,
    REVIEW_PHYSICAL,
    REVIEW_PHYSICAL,
    FIRST,
    SECOND,
    current,
  )
  assert.equal(late.ok, true)
  assert.equal(review.satisfiesGuard(TREE, late.value), false)
})

test('WHAT[REVIEW-ASSURANCE-004] confirmed_review_witness_is_pure_projection_from_durable_facts', () => {
  const memberWitnesses = [
    { reviewer: 'ses_rev_1', barrier: 'bar_1', witness: confirmed({ first: review.verdictWitness({ run: 'r1', call: 'c1', tree: TREE, reviewer: 'ses_rev_1' }), second: review.verdictWitness({ run: 'r2', call: 'c2', tree: TREE, reviewer: 'ses_rev_1' }) }) },
    { reviewer: 'ses_rev_2', barrier: 'bar_2', witness: review.confirmWitness('bar_2', REVIEW_PHYSICAL, REVIEW_PHYSICAL, review.verdictWitness({ run: 'r3', call: 'c3', tree: TREE, reviewer: 'ses_rev_2' }), review.verdictWitness({ run: 'r4', call: 'c4', tree: TREE, reviewer: 'ses_rev_2' })) },
  ]
  const projected = review.projectConfirmedReview('life_1', 'req_1', TREE, memberWitnesses)
  assert.equal(projected.ok, true)
  assert.equal(review.confirmedReviewWitnessTree(projected.witness), TREE)

  const incomplete = [
    { reviewer: 'ses_rev_1', barrier: 'bar_1', witness: confirmed() },
  ]
  const failedProjection = review.projectConfirmedReview('life_1', 'req_1', TREE, incomplete)
  assert.equal(failedProjection.ok, false)
})

test('WHAT[REVIEW-ASSURANCE-005] confirmed_review_witness_binds_tree_and_contains_cohort_evidence', () => {
  const memberWitnesses = [
    { reviewer: 'ses_rev_1', barrier: 'bar_1', witness: confirmed({ first: review.verdictWitness({ run: 'r1', call: 'c1', tree: TREE, reviewer: 'ses_rev_1' }), second: review.verdictWitness({ run: 'r2', call: 'c2', tree: TREE, reviewer: 'ses_rev_1' }) }) },
    { reviewer: 'ses_rev_2', barrier: 'bar_2', witness: review.confirmWitness('bar_2', REVIEW_PHYSICAL, REVIEW_PHYSICAL, review.verdictWitness({ run: 'r3', call: 'c3', tree: TREE, reviewer: 'ses_rev_2' }), review.verdictWitness({ run: 'r4', call: 'c4', tree: TREE, reviewer: 'ses_rev_2' })) },
  ]
  const projected = review.projectConfirmedReview('life_1', 'req_1', TREE, memberWitnesses)
  assert.equal(projected.ok, true)
  assert.equal(review.isConfirmedReviewValidForTree(TREE, projected.witness), true)
  assert.equal(review.isConfirmedReviewValidForTree(OTHER_TREE, projected.witness), false)
})

test('WHAT[REVIEW-ASSURANCE-006] candidate_verification_verifies_candidate_tree_and_rejects_stale_witness', () => {
  const memberWitnesses = [
    { reviewer: 'ses_rev_1', barrier: 'bar_1', witness: confirmed({ first: review.verdictWitness({ run: 'r1', call: 'c1', tree: TREE, reviewer: 'ses_rev_1' }), second: review.verdictWitness({ run: 'r2', call: 'c2', tree: TREE, reviewer: 'ses_rev_1' }) }) },
    { reviewer: 'ses_rev_2', barrier: 'bar_2', witness: review.confirmWitness('bar_2', REVIEW_PHYSICAL, REVIEW_PHYSICAL, review.verdictWitness({ run: 'r3', call: 'c3', tree: TREE, reviewer: 'ses_rev_2' }), review.verdictWitness({ run: 'r4', call: 'c4', tree: TREE, reviewer: 'ses_rev_2' })) },
  ]
  const projected = review.projectConfirmedReview('life_1', 'req_1', TREE, memberWitnesses)
  assert.equal(projected.ok, true)

  const candidateOk = review.verifyCandidate(TREE, projected.witness)
  assert.equal(candidateOk.ok, true)
  const candidateStale = review.verifyCandidate(OTHER_TREE, projected.witness)
  assert.equal(candidateStale.ok, false)
  assert.equal(candidateStale.error, 'StaleWitness')
  assert.equal(candidateStale.candidateTree, OTHER_TREE)
  assert.equal(candidateStale.witnessTree, TREE)
})
