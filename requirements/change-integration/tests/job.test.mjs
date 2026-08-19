// CHGINT-001/003/004/005/006/007/009/010/012/013 — change projection.

import assert from 'node:assert/strict'
import test from 'node:test'

const change = await import('../../../dist/Change/Surface.js')

const JOB = 'job_1'
const MANAGER = 'ses_m'
const payload = (overrides = {}) => ({
  jobId: JOB,
  managerSessionId: MANAGER,
  managerAgent: 'fast-manager',
  byname: 'Road',
  worktreeIdentity: 'wt_1',
  worktreePath: '/tmp/wt1',
  targetRef: 'refs/heads/main',
  targetBranchFrozen: 'refs/heads/main',
  ...overrides,
})

const created = (overrides) => change.createJob(change.empty(), payload(overrides))
const fact = {
  candidateReady: (candidateCommit = 'c1', barrier = 'bar_1') =>
    change.fact('CandidateReady', { candidateCommit, preRebaseReviewBarrierId: barrier }),
  conflictDetected: (conflictFiles = ['a.fs', 'b.fs']) =>
    change.fact('ConflictDetected', {
      candidateCommit: 'c1',
      targetHeadSnapshot: 'h1',
      conflictFiles,
      diagnosticsDigest: 'digest',
    }),
  rebased: (snapshot = 'h1') =>
    change.fact('RebasedCandidateReady', {
      rebasedCommit: 'r1',
      targetHeadSnapshot: snapshot,
      postRebaseReviewBarrierId: 'bar_2',
    }),
  publishClaimed: () => change.fact('PublishClaimed', { rebasedCommit: 'r1', expectedHead: 'h1' }),
  published: () => change.fact('Published', { candidateCommit: 'c1', resultingTargetHead: 'r1' }),
  failed: (reason = 'boom') => change.fact('JobFailed', { reason }),
  abandoned: () => change.fact('JobAbandoned', null),
}

const jobAt = (value, projection = created()) => {
  const next = change.recordFact(projection, JOB, value)
  return { projection: next, job: change.find(next, JOB) }
}
const classifyRebased = (head, rebasedCommit = 'r1', snapshot = 'h1') =>
  change.classifyRebasedCandidate(head ?? null, rebasedCommit, snapshot)
const classifyClaim = (head, rebasedCommit = 'r1', expectedHead = 'h1') =>
  change.classifyPublishClaim(head ?? null, rebasedCommit, expectedHead)

// ── one job, one worktree, one Manager ────────────────────────────────────────

test('WHAT[CHGINT-001] ORCH_003_a_created_job_persists_the_manager_agent_and_the_worktree_identity', () => {
  const job = change.find(created(), JOB)
  assert.deepEqual(job, {
    jobId: 'job_1',
    managerSessionId: 'ses_m',
    managerAgent: 'fast-manager',
    byname: 'Road',
    worktreeIdentity: 'wt_1',
    worktreePath: '/tmp/wt1',
    targetRef: 'refs/heads/main',
    targetBranchFrozen: 'refs/heads/main',
    facts: [],
  })
})

test('WHAT[CHGINT-009] ORCH_006_the_worktree_is_located_by_identity_and_the_path_is_only_diagnostic', () => {
  const job = change.find(created(), JOB)
  assert.notEqual(job.worktreeIdentity, job.worktreePath)
  assert.equal(job.worktreeIdentity, 'wt_1')
})

test('WHAT[CHGINT-009] ORCH_003_durable_facts_do_not_change_job_identity', () => {
  const before = change.find(created(), JOB)
  const after = jobAt(fact.candidateReady())
  const identity = (job) => ({
    jobId: job.jobId,
    managerSessionId: job.managerSessionId,
    managerAgent: job.managerAgent,
    worktreeIdentity: job.worktreeIdentity,
    worktreePath: job.worktreePath,
    targetRef: job.targetRef,
    targetBranchFrozen: job.targetBranchFrozen,
  })
  assert.deepEqual(identity(after.job), identity(before))
  assert.deepEqual(after.job.facts, ['CandidateReady'])
})

test('WHAT[CHGINT-006] ORCH_003_fact_for_an_unknown_job_is_a_no_op_rather_than_a_new_entry', () => {
  const projection = change.recordFact(created(), 'never', fact.candidateReady())
  assert.equal(change.activeJobs(projection).length, 1)
  assert.equal(change.find(projection, 'never'), null)
})

test('WHAT[CHGINT-009] ORCH_003_a_manager_session_resolves_to_its_one_job', () => {
  const first = created()
  const second = change.createJob(first, payload({ jobId: 'job_2', managerSessionId: 'ses_m2', worktreeIdentity: 'wt_2' }))
  assert.equal(change.find(second, 'job_2').managerSessionId, 'ses_m2')
  assert.equal(change.find(second, 'ses_zz'), null)
})

// ── independent jobs and terminal facts ─────────────────────────────────────

test('WHAT[CHGINT-004] ORCH_004_multiple_jobs_are_active_at_once_and_terminal_ones_drop_out', () => {
  let projection = created()
  projection = change.createJob(projection, payload({ jobId: 'job_2', managerSessionId: 'ses_m2' }))
  projection = change.createJob(projection, payload({ jobId: 'job_3', managerSessionId: 'ses_m3' }))
  assert.equal(change.activeJobs(projection).length, 3)

  const finished = change.recordFact(projection, 'job_2', fact.published())
  assert.deepEqual(change.activeJobs(finished).map((job) => job.jobId).sort(), ['job_1', 'job_3'])
})

test('WHAT[CHGINT-006] ORCH_006_a_terminal_job_stays_in_the_map_so_a_replay_is_recognised', () => {
  const published = change.recordFact(created(), JOB, fact.published())
  assert.notEqual(change.find(published, JOB), null)
  assert.equal(change.activeJobs(published).length, 0)
  const replayed = change.recordFact(published, JOB, fact.published())
  assert.deepEqual(change.find(replayed, JOB).facts, ['Published'])
})

test('WHAT[CHGINT-006] ORCH_006_a_terminal_job_accepts_no_further_facts', () => {
  const published = change.recordFact(created(), JOB, fact.published())
  for (const later of [fact.candidateReady('c9'), fact.rebased(), fact.failed('late')]) {
    const after = change.recordFact(published, JOB, later)
    assert.deepEqual(change.find(after, JOB).facts, ['Published'])
  }
})

test('WHAT[CHGINT-006] ORCH_006_all_three_terminal_cases_end_the_job', () => {
  for (const terminal of [fact.published(), fact.failed(), fact.abandoned()]) {
    const projection = change.recordFact(created(), JOB, terminal)
    assert.equal(change.activeJobs(projection).length, 0)
  }
})

// ── ORCH-007 independent durable evidence ───────────────────────────────────

test('WHAT[CHGINT-006] ORCH_007_projection_keeps_independent_facts_instead_of_latest_stage', () => {
  const candidate = jobAt(fact.candidateReady())
  const conflicted = jobAt(fact.conflictDetected(), candidate.projection)
  assert.deepEqual(conflicted.job.facts, ['CandidateReady', 'ConflictDetected'])
})

test('WHAT[CHGINT-005] ORCH_003_a_conflict_goes_back_to_the_same_manager_with_the_conflicted_files', () => {
  const { job } = jobAt(fact.conflictDetected(['src/a.fs', 'src/b.fs']))
  assert.deepEqual(job.facts, ['ConflictDetected'])
  const conflict = change.fact('ConflictDetected', {
    candidateCommit: 'c1',
    targetHeadSnapshot: 'h1',
    conflictFiles: ['src/a.fs', 'src/b.fs'],
    diagnosticsDigest: 'digest',
  })
  assert.deepEqual(conflict.payload.conflictFiles, ['src/a.fs', 'src/b.fs'])
})

test('WHAT[CHGINT-010] ORCH_005_a_rebased_candidate_publishes_only_while_the_target_has_not_moved', () => {
  assert.equal(classifyRebased('h1').kind, 'PublishReady')
  assert.equal(classifyRebased('h2').kind, 'NeedsRebase')
})

test('WHAT[CHGINT-013] REVIEW_008_a_moved_target_discards_the_post_rebase_witness', () => {
  assert.equal(classifyRebased('h2').kind, 'NeedsRebase')
})

test('WHAT[CHGINT-007] ORCH_007_the_three_publish_claim_branches_are_evaluated_in_the_clause_order', () => {
  assert.equal(classifyClaim('r1').kind, 'AlreadyFastForwarded')
  assert.equal(classifyClaim('h1').kind, 'PublishReady')
  assert.equal(classifyClaim('h9').kind, 'ClaimExpired')
})

test('WHAT[CHGINT-007] ORCH_008_an_unreadable_target_head_fails_closed_for_every_head_dependent_case', () => {
  assert.equal(classifyRebased(undefined).kind, 'HeadUnreadable')
  assert.equal(classifyClaim(undefined).kind, 'HeadUnreadable')
})

test('WHAT[CHGINT-003] ORCH_007_each_durable_fact_has_one_projection_slot', () => {
  let projection = created()
  for (const value of [fact.candidateReady(), fact.conflictDetected(), fact.rebased(), fact.publishClaimed()]) {
    projection = change.recordFact(projection, JOB, value)
  }
  assert.deepEqual(change.find(projection, JOB).facts, [
    'CandidateReady',
    'ConflictDetected',
    'RebasedCandidateReady',
    'PublishClaimed',
  ])
})

// ── durable fold and typed worktree effect ───────────────────────────────────

const createdEvent = {
  kind: 'ManagerJobCreated',
  payload: payload(),
}
const candidateEvent = {
  kind: 'CandidateReady',
  payload: { jobId: JOB, candidateCommit: 'c1', preRebaseReviewBarrierId: 'bar_1' },
}
const publishedEvent = {
  kind: 'Published',
  payload: { jobId: JOB, candidateCommit: 'c1', resultingTargetHead: 'r1' },
}
const foldProjection = (events) => {
  const result = change.fold(events)
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.value
}

test('WHAT[CHGINT-006] ORCH_006_the_journal_replays_independent_facts_and_terminal', () => {
  const projection = foldProjection([createdEvent, candidateEvent, publishedEvent])
  assert.deepEqual(change.find(projection, JOB).facts, ['CandidateReady', 'Published'])
  assert.equal(change.activeJobs(projection).length, 0)
})

test('WHAT[CHGINT-006] ORCH_006_a_fact_before_its_create_is_dropped_not_promoted', () => {
  const projection = foldProjection([candidateEvent])
  assert.equal(change.find(projection, JOB), null)
})

test('WHAT[CHGINT-006] ORCH_006_a_replayed_create_does_not_reset_a_job_that_already_made_progress', () => {
  const projection = foldProjection([createdEvent, candidateEvent, publishedEvent, createdEvent])
  assert.deepEqual(change.find(projection, JOB).facts, ['CandidateReady', 'Published'])
  assert.equal(change.activeJobs(projection).length, 0)
})

test('WHAT[CHGINT-009] ORCH_003_a_second_create_for_one_job_id_cannot_change_its_manager_or_worktree', () => {
  const again = change.createJob(created(), payload({ managerAgent: 'deep-manager', worktreeIdentity: 'wt_other' }))
  assert.deepEqual(
    { agent: change.find(again, JOB).managerAgent, worktree: change.find(again, JOB).worktreeIdentity },
    { agent: 'fast-manager', worktree: 'wt_1' },
  )
})

const worktreeRequested = { kind: 'WorktreeCreateRequested', payload: { jobId: JOB, worktreeIdentity: 'manager/job_1', worktreePath: '/tmp/wt1' } }
const worktreeCreated = { kind: 'WorktreeCreated', payload: { jobId: JOB, worktreeIdentity: 'manager/job_1', worktreePath: '/tmp/wt1' } }

test('WHAT[CHGINT-006] PERSIST_009_worktree_request_then_created_marks_identity_created', () => {
  const projection = foldProjection([worktreeRequested, worktreeCreated])
  assert.equal(change.worktreeEffect(projection, 'manager/job_1'), 'Created')
})

test('WHAT[CHGINT-006] PERSIST_009_duplicate_request_after_created_does_not_regress_to_requested', () => {
  const projection = foldProjection([worktreeRequested, worktreeCreated, worktreeRequested])
  assert.equal(change.worktreeEffect(projection, 'manager/job_1'), 'Created')
})

test('WHAT[CHGINT-006] PERSIST_009_duplicate_created_is_idempotent', () => {
  const projection = foldProjection([worktreeRequested, worktreeCreated, worktreeCreated])
  assert.equal(change.worktreeEffect(projection, 'manager/job_1'), 'Created')
})

test('WHAT[CHGINT-006] PERSIST_009_request_alone_is_not_created', () => {
  const projection = foldProjection([worktreeRequested])
  assert.equal(change.worktreeEffect(projection, 'manager/job_1'), 'Requested')
})

test('WHAT[CHGINT-006] PERSIST_009_direct_request_accept_helpers_match_fold', () => {
  let projection = change.empty()
  projection = change.requestWorktree(projection, 'manager/job_1', '/tmp/wt1', JOB)
  assert.equal(change.worktreeEffect(projection, 'manager/job_1'), 'Requested')
  projection = change.acceptWorktree(projection, 'manager/job_1', '/tmp/wt1', JOB)
  assert.equal(change.worktreeEffect(projection, 'manager/job_1'), 'Created')
  projection = change.requestWorktree(projection, 'manager/job_1', '/tmp/wt1', JOB)
  assert.equal(change.worktreeEffect(projection, 'manager/job_1'), 'Created')
})
