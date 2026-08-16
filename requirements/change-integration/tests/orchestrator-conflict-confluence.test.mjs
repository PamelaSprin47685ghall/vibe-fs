// CHGINT-003/004/005/006/007/008/013 — restart and interleaving algebra.

import assert from 'node:assert/strict'
import test from 'node:test'

const change = await import('../../../dist/Change/Surface.js')

const JOB_A = 'job_a'
const JOB_B = 'job_b'

const createEvent = (jobId, managerSessionId, worktreeIdentity = `wt_${jobId.slice(-1)}`) => ({
  kind: 'ManagerJobCreated',
  payload: {
    jobId,
    managerSessionId,
    managerAgent: 'fast-manager',
    byname: 'Road',
    worktreeIdentity,
    worktreePath: `/tmp/${worktreeIdentity}`,
    targetRef: 'refs/heads/main',
    targetBranchFrozen: 'refs/heads/main',
  },
})
const candidateEvent = (jobId, candidateCommit = 'c1', barrier = 'bar_1') => ({
  kind: 'CandidateReady',
  payload: { jobId, candidateCommit, preRebaseReviewBarrierId: barrier },
})
const conflictEvent = (jobId, { candidateCommit = 'c1', targetHeadSnapshot = 'h1', conflictFiles = ['publish_proof.txt'] } = {}) => ({
  kind: 'ConflictDetected',
  payload: { jobId, candidateCommit, targetHeadSnapshot, conflictFiles, diagnosticsDigest: 'conflict-digest' },
})
const rebasedEvent = (jobId, { rebasedCommit = 'r1', targetHeadSnapshot = 'h1', barrier = 'bar_2' } = {}) => ({
  kind: 'RebasedCandidateReady',
  payload: { jobId, rebasedCommit, targetHeadSnapshot, postRebaseReviewBarrierId: barrier },
})
const publishClaimedEvent = (jobId, expectedHead = 'h1') => ({
  kind: 'PublishClaimed',
  payload: { jobId, targetRef: 'refs/heads/main', expectedHead },
})
const publishedEvent = (jobId, candidateCommit = 'c1', resultingTargetHead = 'r1') => ({
  kind: 'Published',
  payload: { jobId, candidateCommit, resultingTargetHead },
})

const foldEvents = (events) => {
  const result = change.fold(events)
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.value
}
const progressOf = (projection, jobId) => change.find(projection, jobId).progress
const actionOf = (projection, jobId, head) => change.recoveryAction(projection, jobId, head ?? null)

// ── Theorem 1: independent jobs commute ─────────────────────────────────────

test('WHAT[CHGINT-004] THEOREM_orchestrator_independent_jobs_confluent_across_interleavings', () => {
  const seqA = [createEvent(JOB_A, 'ses_orch_a'), candidateEvent(JOB_A, 'ca', 'bar_a')]
  const seqB = [createEvent(JOB_B, 'ses_orch_b'), candidateEvent(JOB_B, 'cb', 'bar_b')]
  const foldAB = foldEvents([...seqA, ...seqB])
  const foldBA = foldEvents([...seqB, ...seqA])

  assert.equal(progressOf(foldAB, JOB_A), 'CandidateReady')
  assert.equal(progressOf(foldAB, JOB_B), 'CandidateReady')
  assert.equal(progressOf(foldBA, JOB_A), 'CandidateReady')
  assert.equal(progressOf(foldBA, JOB_B), 'CandidateReady')
  assert.equal(actionOf(foldAB, JOB_A, 'h1').kind, 'RebaseReviewPublish')
  assert.equal(actionOf(foldBA, JOB_B, 'h1').kind, 'RebaseReviewPublish')

  for (const interleaving of [
    [...seqA, ...seqB],
    [seqA[0], seqB[0], seqA[1], seqB[1]],
    [seqB[0], seqA[0], seqB[1], seqA[1]],
    [...seqB, ...seqA],
  ]) {
    const folded = foldEvents(interleaving)
    assert.equal(progressOf(folded, JOB_A), progressOf(foldAB, JOB_A))
    assert.equal(progressOf(folded, JOB_B), progressOf(foldAB, JOB_B))
    assert.equal(actionOf(folded, JOB_A, 'h1').kind, 'RebaseReviewPublish')
    assert.equal(actionOf(folded, JOB_B, 'h1').kind, 'RebaseReviewPublish')
  }
})

test('WHAT[CHGINT-005] THEOREM_conflict_detected_folds_to_resume_conflict_resolution', () => {
  const folded = foldEvents([
    createEvent(JOB_A, 'ses_orch_a'),
    candidateEvent(JOB_A),
    conflictEvent(JOB_A, { conflictFiles: ['publish_proof.txt', 'src/a.fs'] }),
  ])
  assert.equal(progressOf(folded, JOB_A), 'ConflictPending')
  assert.equal(actionOf(folded, JOB_A, undefined).kind, 'ResumeConflictResolution')
  assert.equal(actionOf(folded, JOB_A, 'h9').kind, 'ResumeConflictResolution')
  assert.deepEqual(
    { commit: actionOf(folded, JOB_A, 'h1').candidateCommit, files: actionOf(folded, JOB_A, 'h1').conflictFiles },
    { commit: 'c1', files: ['publish_proof.txt', 'src/a.fs'] },
  )
})

test('WHAT[CHGINT-007] THEOREM_publish_claimed_three_branch_order_is_fixed', () => {
  const folded = foldEvents([
    createEvent(JOB_A, 'ses_orch_a'),
    candidateEvent(JOB_A),
    rebasedEvent(JOB_A),
    publishClaimedEvent(JOB_A),
  ])
  assert.equal(progressOf(folded, JOB_A), 'PublishClaimed')
  assert.deepEqual(actionOf(folded, JOB_A, 'r1'), { kind: 'BackfillPublished', rebasedCommit: 'r1', resultingTargetHead: 'r1' })
  assert.deepEqual(actionOf(folded, JOB_A, 'h1'), { kind: 'AttemptPublish', rebasedCommit: 'r1', expectedHead: 'h1' })
  assert.equal(actionOf(folded, JOB_A, 'h9').kind, 'RebaseAndReviewAgain')
})

test('WHAT[CHGINT-013] THEOREM_stale_target_on_rebased_candidate_discards_witness', () => {
  const folded = foldEvents([createEvent(JOB_A, 'ses_orch_a'), candidateEvent(JOB_A), rebasedEvent(JOB_A)])
  assert.equal(progressOf(folded, JOB_A), 'RebasedCandidateReady')
  assert.equal(actionOf(folded, JOB_A, 'h1').kind, 'AttemptPublish')
  assert.equal(actionOf(folded, JOB_A, 'h2').kind, 'RebaseAndReviewAgain')
})

test('WHAT[CHGINT-008] THEOREM_unreadable_target_head_fails_closed', () => {
  for (const events of [
    [createEvent(JOB_A, 'ses_orch_a'), candidateEvent(JOB_A), rebasedEvent(JOB_A)],
    [createEvent(JOB_A, 'ses_orch_a'), candidateEvent(JOB_A), rebasedEvent(JOB_A), publishClaimedEvent(JOB_A)],
  ]) {
    const action = actionOf(foldEvents(events), JOB_A, undefined)
    assert.equal(action.kind, 'FailClosed')
    assert.equal(action.reason, 'GetTargetHead failed; ORCH-008 forbids falling back to HEAD')
  }
})

test('WHAT[CHGINT-006] THEOREM_latest_progress_wins_and_published_is_terminal', () => {
  const events = [
    createEvent(JOB_A, 'ses_orch_a'),
    candidateEvent(JOB_A),
    conflictEvent(JOB_A),
    rebasedEvent(JOB_A),
    publishClaimedEvent(JOB_A),
    publishedEvent(JOB_A),
  ]
  const folded = foldEvents(events)
  assert.equal(progressOf(folded, JOB_A), 'Published')
  assert.equal(change.activeJobs(folded).length, 0)
  assert.equal(actionOf(folded, JOB_A, 'r1').kind, 'CleanUp')

  const replayCreate = foldEvents([...events, createEvent(JOB_A, 'ses_orch_a')])
  assert.equal(progressOf(replayCreate, JOB_A), 'Published')
  assert.equal(change.activeJobs(replayCreate).length, 0)
  assert.equal(actionOf(replayCreate, JOB_A, 'r1').kind, 'CleanUp')
})

test('WHAT[CHGINT-003] THEOREM_publish_claimed_without_rebased_candidate_is_rejected', () => {
  const result = change.fold([createEvent(JOB_A, 'ses_orch_a'), candidateEvent(JOB_A), publishClaimedEvent(JOB_A)])
  assert.equal(result.ok, false)
  assert.match(result.error, /no rebased candidate/)
})

test('WHAT[CHGINT-005] THEOREM_drop_ephemeral_preserves_conflict_pending_recovery', () => {
  const durable = [createEvent(JOB_A, 'ses_orch_a'), conflictEvent(JOB_A)]
  const before = foldEvents(durable)
  assert.equal(progressOf(before, JOB_A), 'ConflictPending')
  assert.equal(actionOf(before, JOB_A, 'h1').kind, 'ResumeConflictResolution')

  const after = foldEvents(durable)
  assert.equal(progressOf(after, JOB_A), 'ConflictPending')
  assert.equal(actionOf(after, JOB_A, 'h1').kind, 'ResumeConflictResolution')
  assert.deepEqual(
    { commit: actionOf(after, JOB_A, 'h1').candidateCommit, files: actionOf(after, JOB_A, 'h1').conflictFiles },
    { commit: 'c1', files: ['publish_proof.txt'] },
  )
})

test('WHAT[CHGINT-007] THEOREM_drop_ephemeral_preserves_publish_claimed_branch_algebra', () => {
  const durable = [createEvent(JOB_A, 'ses_orch_a'), candidateEvent(JOB_A), rebasedEvent(JOB_A), publishClaimedEvent(JOB_A)]
  const before = foldEvents(durable)
  assert.equal(progressOf(before, JOB_A), 'PublishClaimed')
  assert.equal(actionOf(before, JOB_A, 'r1').kind, 'BackfillPublished')
  assert.equal(actionOf(before, JOB_A, 'h1').kind, 'AttemptPublish')
  assert.equal(actionOf(before, JOB_A, 'h9').kind, 'RebaseAndReviewAgain')

  const after = foldEvents(durable)
  assert.equal(progressOf(after, JOB_A), 'PublishClaimed')
  assert.equal(actionOf(after, JOB_A, 'r1').kind, 'BackfillPublished')
  assert.equal(actionOf(after, JOB_A, 'h1').kind, 'AttemptPublish')
  assert.equal(actionOf(after, JOB_A, 'h9').kind, 'RebaseAndReviewAgain')
  assert.equal(actionOf(after, JOB_A, undefined).kind, 'FailClosed')
})
