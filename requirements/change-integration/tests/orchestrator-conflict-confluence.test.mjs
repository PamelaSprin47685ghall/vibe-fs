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
    managerAgent: 'manager',
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
const factsOf = (projection, jobId) => change.find(projection, jobId).facts
const classifyRebased = (head, rebasedCommit = 'r1', snapshot = 'h1') =>
  change.classifyRebasedCandidate(head ?? null, rebasedCommit, snapshot)
const classifyClaim = (head, rebasedCommit = 'r1', expectedHead = 'h1') =>
  change.classifyPublishClaim(head ?? null, rebasedCommit, expectedHead)

// ── Theorem 1: independent jobs commute ─────────────────────────────────────

test('WHAT[CHGINT-004] THEOREM_orchestrator_independent_jobs_confluent_across_interleavings', () => {
  const seqA = [createEvent(JOB_A, 'ses_orch_a'), candidateEvent(JOB_A, 'ca', 'bar_a')]
  const seqB = [createEvent(JOB_B, 'ses_orch_b'), candidateEvent(JOB_B, 'cb', 'bar_b')]
  const foldAB = foldEvents([...seqA, ...seqB])
  const foldBA = foldEvents([...seqB, ...seqA])

  assert.deepEqual(factsOf(foldAB, JOB_A), ['CandidateReady'])
  assert.deepEqual(factsOf(foldAB, JOB_B), ['CandidateReady'])
  assert.deepEqual(factsOf(foldBA, JOB_A), ['CandidateReady'])
  assert.deepEqual(factsOf(foldBA, JOB_B), ['CandidateReady'])

  for (const interleaving of [
    [...seqA, ...seqB],
    [seqA[0], seqB[0], seqA[1], seqB[1]],
    [seqB[0], seqA[0], seqB[1], seqA[1]],
    [...seqB, ...seqA],
  ]) {
    const folded = foldEvents(interleaving)
    assert.deepEqual(factsOf(folded, JOB_A), factsOf(foldAB, JOB_A))
    assert.deepEqual(factsOf(folded, JOB_B), factsOf(foldAB, JOB_B))
  }
})

test('WHAT[CHGINT-005] THEOREM_conflict_detected_remains_independent_durable_evidence', () => {
  const folded = foldEvents([
    createEvent(JOB_A, 'ses_orch_a'),
    candidateEvent(JOB_A),
    conflictEvent(JOB_A, { conflictFiles: ['publish_proof.txt', 'src/a.fs'] }),
  ])
  assert.deepEqual(factsOf(folded, JOB_A), ['CandidateReady', 'ConflictDetected'])
})

test('WHAT[CHGINT-007] THEOREM_publish_claimed_three_branch_order_is_fixed', () => {
  const folded = foldEvents([
    createEvent(JOB_A, 'ses_orch_a'),
    candidateEvent(JOB_A),
    rebasedEvent(JOB_A),
    publishClaimedEvent(JOB_A),
  ])
  assert.deepEqual(factsOf(folded, JOB_A), ['CandidateReady', 'RebasedCandidateReady', 'PublishClaimed'])
  assert.equal(classifyClaim('r1').kind, 'AlreadyFastForwarded')
  assert.equal(classifyClaim('h1').kind, 'PublishReady')
  assert.equal(classifyClaim('h9').kind, 'ClaimExpired')
})

test('WHAT[CHGINT-013] THEOREM_stale_target_on_rebased_candidate_discards_witness', () => {
  const folded = foldEvents([createEvent(JOB_A, 'ses_orch_a'), candidateEvent(JOB_A), rebasedEvent(JOB_A)])
  assert.deepEqual(factsOf(folded, JOB_A), ['CandidateReady', 'RebasedCandidateReady'])
  assert.equal(classifyRebased('h1').kind, 'PublishReady')
  assert.equal(classifyRebased('h2').kind, 'NeedsRebase')
})

test('WHAT[CHGINT-008] THEOREM_unreadable_target_head_fails_closed', () => {
  assert.equal(classifyRebased(undefined).kind, 'HeadUnreadable')
  assert.equal(classifyClaim(undefined).kind, 'HeadUnreadable')
})

test('WHAT[CHGINT-006] THEOREM_independent_facts_survive_and_published_is_terminal', () => {
  const events = [
    createEvent(JOB_A, 'ses_orch_a'),
    candidateEvent(JOB_A),
    conflictEvent(JOB_A),
    rebasedEvent(JOB_A),
    publishClaimedEvent(JOB_A),
    publishedEvent(JOB_A),
  ]
  const folded = foldEvents(events)
  assert.deepEqual(factsOf(folded, JOB_A), [
    'CandidateReady',
    'ConflictDetected',
    'RebasedCandidateReady',
    'PublishClaimed',
    'Published',
  ])
  assert.equal(change.activeJobs(folded).length, 0)

  const replayCreate = foldEvents([...events, createEvent(JOB_A, 'ses_orch_a')])
  assert.deepEqual(factsOf(replayCreate, JOB_A), factsOf(folded, JOB_A))
  assert.equal(change.activeJobs(replayCreate).length, 0)
})

test('WHAT[CHGINT-003] THEOREM_publish_claimed_without_rebased_candidate_is_rejected', () => {
  const result = change.fold([createEvent(JOB_A, 'ses_orch_a'), candidateEvent(JOB_A), publishClaimedEvent(JOB_A)])
  assert.equal(result.ok, false)
  assert.match(result.error, /no rebased candidate/)
})

test('WHAT[CHGINT-005] THEOREM_drop_ephemeral_preserves_conflict_evidence', () => {
  const durable = [createEvent(JOB_A, 'ses_orch_a'), conflictEvent(JOB_A)]
  const before = foldEvents(durable)
  assert.deepEqual(factsOf(before, JOB_A), ['ConflictDetected'])

  const after = foldEvents(durable)
  assert.deepEqual(factsOf(after, JOB_A), ['ConflictDetected'])
})

test('WHAT[CHGINT-007] THEOREM_drop_ephemeral_preserves_publish_claimed_branch_algebra', () => {
  const durable = [createEvent(JOB_A, 'ses_orch_a'), candidateEvent(JOB_A), rebasedEvent(JOB_A), publishClaimedEvent(JOB_A)]
  const before = foldEvents(durable)
  assert.deepEqual(factsOf(before, JOB_A), ['CandidateReady', 'RebasedCandidateReady', 'PublishClaimed'])
  assert.equal(classifyClaim('r1').kind, 'AlreadyFastForwarded')
  assert.equal(classifyClaim('h1').kind, 'PublishReady')
  assert.equal(classifyClaim('h9').kind, 'ClaimExpired')

  const after = foldEvents(durable)
  assert.deepEqual(factsOf(after, JOB_A), ['CandidateReady', 'RebasedCandidateReady', 'PublishClaimed'])
  assert.equal(classifyClaim('r1').kind, 'AlreadyFastForwarded')
  assert.equal(classifyClaim('h1').kind, 'PublishReady')
  assert.equal(classifyClaim('h9').kind, 'ClaimExpired')
  assert.equal(classifyClaim(undefined).kind, 'HeadUnreadable')
})
