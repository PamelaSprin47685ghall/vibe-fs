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
const progress = {
  managerStarted: () => change.progress('ManagerStarted', null),
  candidateReady: (candidateCommit = 'c1', barrier = 'bar_1') =>
    change.progress('CandidateReady', { candidateCommit, preRebaseReviewBarrierId: barrier }),
  conflictPending: (conflictFiles = ['a.fs', 'b.fs']) =>
    change.progress('ConflictPending', {
      candidateCommit: 'c1',
      targetHeadSnapshot: 'h1',
      conflictFiles,
      diagnosticsDigest: 'digest',
    }),
  rebased: (snapshot = 'h1') =>
    change.progress('RebasedCandidateReady', {
      rebasedCommit: 'r1',
      targetHeadSnapshot: snapshot,
      postRebaseReviewBarrierId: 'bar_2',
    }),
  publishClaimed: () => change.progress('PublishClaimed', { rebasedCommit: 'r1', expectedHead: 'h1' }),
  published: () => change.progress('Published', { candidateCommit: 'c1', resultingTargetHead: 'r1' }),
  failed: (reason = 'boom') => change.progress('Failed', reason),
  abandoned: () => change.progress('Abandoned', null),
}

const jobAt = (value, projection = created()) => {
  const next = change.recordProgress(projection, JOB, value)
  return { projection: next, job: change.find(next, JOB) }
}
const actionAt = (value, head) => change.recoveryAction(value.projection, JOB, head ?? null)
const actionKind = (value, head) => actionAt(value, head).kind

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
    progress: 'ManagerStarted',
  })
})

test('WHAT[CHGINT-009] ORCH_006_the_worktree_is_located_by_identity_and_the_path_is_only_diagnostic', () => {
  const job = change.find(created(), JOB)
  assert.notEqual(job.worktreeIdentity, job.worktreePath)
  assert.equal(job.worktreeIdentity, 'wt_1')
})

test('WHAT[CHGINT-009] ORCH_003_only_progress_ever_changes_after_creation', () => {
  const before = change.find(created(), JOB)
  const after = jobAt(progress.candidateReady())
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
  assert.notEqual(after.job.progress, before.progress)
})

test('WHAT[CHGINT-006] ORCH_003_progress_for_an_unknown_job_is_a_no_op_rather_than_a_new_entry', () => {
  const projection = change.recordProgress(created(), 'never', progress.candidateReady())
  assert.equal(change.activeJobs(projection).length, 1)
  assert.equal(change.find(projection, 'never'), null)
})

test('WHAT[CHGINT-009] ORCH_003_a_manager_session_resolves_to_its_one_job', () => {
  const first = created()
  const second = change.createJob(first, payload({ jobId: 'job_2', managerSessionId: 'ses_m2', worktreeIdentity: 'wt_2' }))
  assert.equal(change.find(second, 'job_2').managerSessionId, 'ses_m2')
  assert.equal(change.find(second, 'ses_zz'), null)
})

// ── independent jobs and terminal progress ──────────────────────────────────

test('WHAT[CHGINT-004] ORCH_004_multiple_jobs_are_active_at_once_and_terminal_ones_drop_out', () => {
  let projection = created()
  projection = change.createJob(projection, payload({ jobId: 'job_2', managerSessionId: 'ses_m2' }))
  projection = change.createJob(projection, payload({ jobId: 'job_3', managerSessionId: 'ses_m3' }))
  assert.equal(change.activeJobs(projection).length, 3)

  const finished = change.recordProgress(projection, 'job_2', progress.published())
  assert.deepEqual(change.activeJobs(finished).map((job) => job.jobId).sort(), ['job_1', 'job_3'])
})

test('WHAT[CHGINT-006] ORCH_006_a_terminal_job_stays_in_the_map_so_a_replay_is_recognised', () => {
  const published = change.recordProgress(created(), JOB, progress.published())
  assert.notEqual(change.find(published, JOB), null)
  assert.equal(change.activeJobs(published).length, 0)
  const replayed = change.recordProgress(published, JOB, progress.published())
  assert.equal(change.find(replayed, JOB).progress, 'Published')
})

test('WHAT[CHGINT-006] ORCH_006_a_terminal_job_accepts_no_further_progress', () => {
  const published = change.recordProgress(created(), JOB, progress.published())
  for (const later of [progress.candidateReady('c9'), progress.rebased(), progress.failed('late')]) {
    const after = change.recordProgress(published, JOB, later)
    assert.equal(change.find(after, JOB).progress, 'Published')
  }
})

test('WHAT[CHGINT-006] ORCH_006_all_three_terminal_cases_end_the_job', () => {
  for (const terminal of [progress.published(), progress.failed(), progress.abandoned()]) {
    const projection = change.recordProgress(created(), JOB, terminal)
    assert.equal(change.activeJobs(projection).length, 0)
    assert.equal(change.recoveryAction(projection, JOB, 'h1').kind, 'CleanUp')
  }
})

// ── ORCH-007 recovery action totality ─────────────────────────────────────────

test('WHAT[CHGINT-012] ORCH_007_progress_that_needs_no_head_derives_its_action_from_the_fact_alone', () => {
  assert.equal(actionKind(jobAt(progress.managerStarted()), undefined), 'ResumeManager')
  assert.equal(actionKind(jobAt(progress.candidateReady()), undefined), 'RebaseReviewPublish')
  assert.equal(actionKind(jobAt(progress.conflictPending()), undefined), 'ResumeConflictResolution')
  assert.equal(actionKind(jobAt(progress.managerStarted()), 'h1'), 'ResumeManager')
  assert.equal(actionKind(jobAt(progress.candidateReady()), 'h1'), 'RebaseReviewPublish')
})

test('WHAT[CHGINT-005] ORCH_003_a_conflict_goes_back_to_the_same_manager_with_the_conflicted_files', () => {
  const action = actionAt(jobAt(progress.conflictPending(['src/a.fs', 'src/b.fs'])), 'h1')
  assert.equal(action.kind, 'ResumeConflictResolution')
  assert.deepEqual({ commit: action.candidateCommit, files: action.conflictFiles }, { commit: 'c1', files: ['src/a.fs', 'src/b.fs'] })
})

test('WHAT[CHGINT-010] ORCH_005_a_rebased_candidate_publishes_only_while_the_target_has_not_moved', () => {
  const action = actionAt(jobAt(progress.rebased('h1')), 'h1')
  assert.equal(action.kind, 'AttemptPublish')
  assert.deepEqual({ rebased: action.rebasedCommit, expected: action.expectedHead }, { rebased: 'r1', expected: 'h1' })
})

test('WHAT[CHGINT-013] REVIEW_008_a_moved_target_discards_the_post_rebase_witness', () => {
  assert.equal(actionKind(jobAt(progress.rebased('h1')), 'h2'), 'RebaseAndReviewAgain')
})

test('WHAT[CHGINT-007] ORCH_007_the_three_publish_claim_branches_are_evaluated_in_the_clause_order', () => {
  const value = jobAt(progress.publishClaimed())
  assert.equal(actionKind(value, 'r1'), 'BackfillPublished')
  assert.deepEqual(actionAt(value, 'r1'), { kind: 'BackfillPublished', rebasedCommit: 'r1', resultingTargetHead: 'r1' })
  assert.equal(actionKind(value, 'h1'), 'AttemptPublish')
  assert.deepEqual(actionAt(value, 'h1'), { kind: 'AttemptPublish', rebasedCommit: 'r1', expectedHead: 'h1' })
  assert.equal(actionKind(value, 'h9'), 'RebaseAndReviewAgain')
})

test('WHAT[CHGINT-007] ORCH_008_an_unreadable_target_head_fails_closed_for_every_head_dependent_case', () => {
  for (const value of [progress.rebased(), progress.publishClaimed()]) {
    const action = actionAt(jobAt(value), undefined)
    assert.equal(action.kind, 'FailClosed')
    assert.equal(action.reason, 'GetTargetHead failed; ORCH-008 forbids falling back to HEAD')
  }
})

test('WHAT[CHGINT-003] ORCH_007_every_progress_case_yields_exactly_one_action', () => {
  const table = [
    ['ManagerStarted', progress.managerStarted(), 'ResumeManager'],
    ['CandidateReady', progress.candidateReady(), 'RebaseReviewPublish'],
    ['ConflictPending', progress.conflictPending(), 'ResumeConflictResolution'],
    ['RebasedCandidateReady', progress.rebased('h1'), 'AttemptPublish'],
    ['PublishClaimed', progress.publishClaimed(), 'AttemptPublish'],
    ['Published', progress.published(), 'CleanUp'],
    ['Failed', progress.failed(), 'CleanUp'],
    ['Abandoned', progress.abandoned(), 'CleanUp'],
  ]
  assert.deepEqual(table.map(([name, value]) => [name, actionKind(jobAt(value), 'h1')]), table.map(([name, , expected]) => [name, expected]))
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

test('WHAT[CHGINT-006] ORCH_006_the_journal_replays_into_the_latest_progress_only', () => {
  const projection = foldProjection([createdEvent, candidateEvent, publishedEvent])
  assert.equal(change.find(projection, JOB).progress, 'Published')
  assert.equal(change.activeJobs(projection).length, 0)
})

test('WHAT[CHGINT-006] ORCH_006_a_progress_fact_before_its_create_is_dropped_not_promoted', () => {
  const projection = foldProjection([candidateEvent])
  assert.equal(change.find(projection, JOB), null)
})

test('WHAT[CHGINT-006] ORCH_006_a_replayed_create_does_not_reset_a_job_that_already_made_progress', () => {
  const projection = foldProjection([createdEvent, candidateEvent, publishedEvent, createdEvent])
  assert.equal(change.find(projection, JOB).progress, 'Published')
  assert.equal(change.activeJobs(projection).length, 0)
  assert.equal(change.recoveryAction(projection, JOB, 'r1').kind, 'CleanUp')
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
