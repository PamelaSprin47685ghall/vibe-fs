// tests/unit/Orchestrator/job.test.mjs — ORCH-003/004/005/006/007/008.
//
// ORCH-006 forbids a fact set where the recovery action is ambiguous, and
// ORCH-007 is the function that must therefore be total: exactly one action per
// job, derived by matching ONE value.
//
// The projection this replaced held five independent optional fields
// (PreRebaseReviewCommit, RebasedCommit, ConflictFiles, PostRebaseReviewCommit,
// PublishClaimHead), so recovery had to rank whichever combination happened to be
// set — and "candidate registered" could mean either "waiting for review" or
// "ready to publish". So the tests below are mostly exhaustiveness: every progress
// case, and for the head-dependent ones every branch of the head comparison.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  commitHash,
  envelope,
  fact,
  fold,
  idValue,
  isSome,
  jobProgress,
  listItems,
  managerJobId,
  orchestratorProjection,
  reviewBarrierId,
  sessionId,
  stream,
  targetRef,
  toList,
  worktreeIdentity,
  worktreePath,
} from '../domain.mjs'

const JOB = managerJobId('job_1')
const MANAGER = sessionId('ses_m')

const createPayload = (overrides = {}) => ({
  ManagerJobId: JOB,
  ManagerSessionId: MANAGER,
  ManagerAgent: 'fast-manager',
  WorktreeIdentity: worktreeIdentity('wt_1'),
  WorktreePath: worktreePath('/tmp/wt1'),
  TargetRef: targetRef('refs/heads/main'),
  TargetBranchFrozen: 'refs/heads/main',
  ...overrides,
})

const created = (overrides) => orchestratorProjection.createJob(createPayload(overrides), orchestratorProjection.empty)

/** A job sitting at one progress case, ready for a recovery question. */
const jobAt = (progress, projection = created()) =>
  orchestratorProjection.tryFind(JOB, orchestratorProjection.recordProgress(JOB, progress, projection))

const progress = {
  managerStarted: () => jobProgress.of('ManagerStarted'),
  candidateReady: (commit = 'c1', barrier = 'bar_1') =>
    jobProgress.of('CandidateReady', {
      CandidateCommit: commitHash(commit),
      PreRebaseReviewBarrierId: reviewBarrierId(barrier),
    }),
  conflictPending: (files = ['a.fs', 'b.fs']) =>
    jobProgress.of('ConflictPending', {
      CandidateCommit: commitHash('c1'),
      TargetHeadSnapshot: commitHash('h1'),
      ConflictFiles: toList(files),
      DiagnosticsDigest: 'digest',
    }),
  rebased: (snapshot = 'h1') =>
    jobProgress.of('RebasedCandidateReady', {
      RebasedCommit: commitHash('r1'),
      TargetHeadSnapshot: commitHash(snapshot),
      PostRebaseReviewBarrierId: reviewBarrierId('bar_2'),
    }),
  publishClaimed: () =>
    jobProgress.of('PublishClaimed', { RebasedCommit: commitHash('r1'), ExpectedHead: commitHash('h1') }),
  published: () =>
    jobProgress.of('Published', { CandidateCommit: commitHash('c1'), ResultingTargetHead: commitHash('r1') }),
  failed: (reason = 'boom') => jobProgress.of('Failed', reason),
  abandoned: () => jobProgress.of('Abandoned'),
}

const actionAt = (head, job) => orchestratorProjection.recoveryAction(head === undefined ? undefined : commitHash(head), job)
const payloadAt = (head, job) =>
  orchestratorProjection.recoveryActionPayload(head === undefined ? undefined : commitHash(head), job)

// ── ORCH-003: one job, one worktree, one Manager, for life ───────────────────

test('ORCH_003_a_created_job_persists_the_manager_agent_and_the_worktree_identity', () => {
  const job = orchestratorProjection.tryFind(JOB, created())

  // `ManagerAgent` is the exact agent, not a bare role: recovery must restore
  // `fast-manager` rather than degrade to "some manager". Package B deleted the
  // branch that fell back to `fast-manager` on a miss and made it an error.
  assert.deepEqual(
    {
      job: idValue.managerJob(job.ManagerJobId),
      manager: idValue.session(job.ManagerSessionId),
      agent: job.ManagerAgent,
      worktree: idValue.worktreeIdentity(job.WorktreeIdentity),
      path: idValue.worktreePath(job.WorktreePath),
      target: idValue.targetRef(job.TargetRef),
      frozen: job.TargetBranchFrozen,
      progress: orchestratorProjection.progressOf(job),
    },
    {
      job: 'job_1',
      manager: 'ses_m',
      agent: 'fast-manager',
      worktree: 'wt_1',
      path: '/tmp/wt1',
      target: 'refs/heads/main',
      frozen: 'refs/heads/main',
      progress: 'ManagerStarted',
    },
  )
})

test('ORCH_006_the_worktree_is_located_by_identity_and_the_path_is_only_diagnostic', () => {
  // Both are recorded, and they are different kinds. A moved worktree must not
  // orphan its job, so recovery keys on the identity; the path is mutable state
  // that exists for a human reading a diagnostic.
  const job = orchestratorProjection.tryFind(JOB, created())

  assert.notEqual(idValue.worktreeIdentity(job.WorktreeIdentity), idValue.worktreePath(job.WorktreePath))
  assert.equal(idValue.worktreeIdentity(job.WorktreeIdentity), 'wt_1')
})

test('ORCH_003_only_progress_ever_changes_after_creation', () => {
  // The worktree and the Manager are fixed for the job's whole life, which is why
  // `recordProgress` replaces exactly one field.
  const before = orchestratorProjection.tryFind(JOB, created())
  const after = jobAt(progress.candidateReady())

  const identity = (job) => ({
    job: idValue.managerJob(job.ManagerJobId),
    manager: idValue.session(job.ManagerSessionId),
    agent: job.ManagerAgent,
    worktree: idValue.worktreeIdentity(job.WorktreeIdentity),
    path: idValue.worktreePath(job.WorktreePath),
    target: idValue.targetRef(job.TargetRef),
    frozen: job.TargetBranchFrozen,
  })

  assert.deepEqual(identity(after), identity(before))
  assert.notEqual(orchestratorProjection.progressOf(after), orchestratorProjection.progressOf(before))
})

test('ORCH_003_progress_for_an_unknown_job_is_a_no_op_rather_than_a_new_entry', () => {
  // Creating a job on the strength of a progress fact would invent one with no
  // worktree and no Manager — the "unknown-job" sentinel shape the domain avoids.
  const projection = orchestratorProjection.recordProgress(
    managerJobId('never'),
    progress.candidateReady(),
    created(),
  )

  assert.equal(orchestratorProjection.activeJobs(projection).length, 1)
  assert.equal(isSome(orchestratorProjection.tryFind(managerJobId('never'), projection)), false)
})

test('ORCH_003_a_manager_session_resolves_to_its_one_job', () => {
  // REVIEW-006 needs `ManagerJobId` and `WorktreeIdentity` inside every confirmed
  // witness, and the reviewer path holds only the session id.
  let projection = created()
  projection = orchestratorProjection.createJob(
    createPayload({
      ManagerJobId: managerJobId('job_2'),
      ManagerSessionId: sessionId('ses_m2'),
      WorktreeIdentity: worktreeIdentity('wt_2'),
    }),
    projection,
  )

  assert.equal(
    idValue.managerJob(orchestratorProjection.tryFindByManagerSession(sessionId('ses_m2'), projection).ManagerJobId),
    'job_2',
  )
  assert.equal(isSome(orchestratorProjection.tryFindByManagerSession(sessionId('ses_zz'), projection)), false)
})

// ── ORCH-004: jobs run in parallel; only the ref mutation serialises ─────────

test('ORCH_004_multiple_jobs_are_active_at_once_and_terminal_ones_drop_out', () => {
  let projection = created()
  projection = orchestratorProjection.createJob(
    createPayload({ ManagerJobId: managerJobId('job_2'), ManagerSessionId: sessionId('ses_m2') }),
    projection,
  )
  projection = orchestratorProjection.createJob(
    createPayload({ ManagerJobId: managerJobId('job_3'), ManagerSessionId: sessionId('ses_m3') }),
    projection,
  )

  assert.equal(orchestratorProjection.activeJobs(projection).length, 3)

  const finished = orchestratorProjection.recordProgress(managerJobId('job_2'), progress.published(), projection)

  assert.deepEqual(
    orchestratorProjection.activeJobs(finished).map((job) => idValue.managerJob(job.ManagerJobId)).sort(),
    ['job_1', 'job_3'],
  )
})

test('ORCH_006_a_terminal_job_stays_in_the_map_so_a_replay_is_recognised', () => {
  // Removing it would make a replayed `Published` create a fresh entry — the job
  // would reopen at ManagerStarted and its Manager would be resumed.
  const published = orchestratorProjection.recordProgress(JOB, progress.published(), created())

  assert.equal(isSome(orchestratorProjection.tryFind(JOB, published)), true)
  assert.equal(orchestratorProjection.activeJobs(published).length, 0)

  const replayed = orchestratorProjection.recordProgress(JOB, progress.published(), published)
  assert.equal(orchestratorProjection.progressOf(orchestratorProjection.tryFind(JOB, replayed)), 'Published')
})

test('ORCH_006_a_terminal_job_accepts_no_further_progress', () => {
  // Not merely idempotent for the same fact: a LATER fact must not reopen it
  // either, or a stale in-flight write could resurrect a finished job.
  const published = orchestratorProjection.recordProgress(JOB, progress.published(), created())

  for (const later of [progress.candidateReady('c9'), progress.rebased(), progress.failed('late')]) {
    const after = orchestratorProjection.recordProgress(JOB, later, published)
    assert.equal(orchestratorProjection.progressOf(orchestratorProjection.tryFind(JOB, after)), 'Published')
  }
})

test('ORCH_006_all_three_terminal_cases_end_the_job', () => {
  for (const terminal of [progress.published(), progress.failed(), progress.abandoned()]) {
    const projection = orchestratorProjection.recordProgress(JOB, terminal, created())
    assert.equal(orchestratorProjection.activeJobs(projection).length, 0)
    assert.equal(actionAt('h1', orchestratorProjection.tryFind(JOB, projection)), 'CleanUp')
  }
})

// ── ORCH-007: exactly one recovery action per job ───────────────────────────

test('ORCH_007_progress_that_needs_no_head_derives_its_action_from_the_fact_alone', () => {
  // These three do not read the target ref at all, so passing no head must still
  // produce the real action rather than fail closed.
  assert.equal(actionAt(undefined, jobAt(progress.managerStarted())), 'ResumeManager')
  assert.equal(actionAt(undefined, jobAt(progress.candidateReady())), 'RebaseReviewPublish')
  assert.equal(actionAt(undefined, jobAt(progress.conflictPending())), 'ResumeConflictResolution')

  // And the same answer with a head present: the head is not an input here.
  assert.equal(actionAt('h1', jobAt(progress.managerStarted())), 'ResumeManager')
  assert.equal(actionAt('h1', jobAt(progress.candidateReady())), 'RebaseReviewPublish')
})

test('ORCH_003_a_conflict_goes_back_to_the_same_manager_with_the_conflicted_files', () => {
  // The clause requires the SAME Manager in the SAME worktree, so the action
  // carries what that Manager needs rather than a flag that says "conflict".
  const job = jobAt(progress.conflictPending(['src/a.fs', 'src/b.fs']))

  assert.equal(actionAt('h1', job), 'ResumeConflictResolution')
  const payload = payloadAt('h1', job)

  assert.deepEqual(
    { commit: idValue.commit(payload.CandidateCommit), files: listItems(payload.ConflictFiles) },
    { commit: 'c1', files: ['src/a.fs', 'src/b.fs'] },
  )
})

test('ORCH_005_a_rebased_candidate_publishes_only_while_the_target_has_not_moved', () => {
  const job = jobAt(progress.rebased('h1'))

  // Unchanged: acquire the short gate and attempt the ff against the head the
  // rebase was computed on.
  assert.equal(actionAt('h1', job), 'AttemptPublish')
  assert.deepEqual(
    {
      rebased: idValue.commit(payloadAt('h1', job).RebasedCommit),
      expected: idValue.commit(payloadAt('h1', job).ExpectedHead),
    },
    { rebased: 'r1', expected: 'h1' },
  )
})

test('REVIEW_008_a_moved_target_discards_the_post_rebase_witness', () => {
  // ORCH-005 explicitly allows the target to move while this job holds no lock.
  // The post-rebase witness is then for the wrong base, so it must not be reused —
  // rebase and review again rather than publish against a base nobody reviewed.
  assert.equal(actionAt('h2', jobAt(progress.rebased('h1'))), 'RebaseAndReviewAgain')
})

test('ORCH_007_the_three_publish_claim_branches_are_evaluated_in_the_clause_order', () => {
  const job = jobAt(progress.publishClaimed())

  // Branch 1 first: the head already IS the rebased commit, so the ff happened
  // and only the fact is missing. Checking "unchanged" first would re-attempt an
  // ff that already succeeded.
  assert.equal(actionAt('r1', job), 'BackfillPublished')
  assert.deepEqual(
    {
      rebased: idValue.commit(payloadAt('r1', job).RebasedCommit),
      head: idValue.commit(payloadAt('r1', job).ResultingTargetHead),
    },
    { rebased: 'r1', head: 'r1' },
  )

  // Branch 2: the head is still what the claim expected, so the ff never ran.
  assert.equal(actionAt('h1', job), 'AttemptPublish')
  assert.deepEqual(
    {
      rebased: idValue.commit(payloadAt('h1', job).RebasedCommit),
      expected: idValue.commit(payloadAt('h1', job).ExpectedHead),
    },
    { rebased: 'r1', expected: 'h1' },
  )

  // Branch 3: somebody else moved it. The claim expired.
  assert.equal(actionAt('h9', job), 'RebaseAndReviewAgain')
})

test('ORCH_008_an_unreadable_target_head_fails_closed_for_every_head_dependent_case', () => {
  // `GetTargetHead` failing must never fall back to HEAD. Both head-dependent
  // cases refuse, and the reason says which clause forbids the fallback.
  for (const value of [progress.rebased(), progress.publishClaimed()]) {
    const job = jobAt(value)
    assert.equal(actionAt(undefined, job), 'FailClosed')
    assert.equal(payloadAt(undefined, job), 'GetTargetHead failed; ORCH-008 forbids falling back to HEAD')
  }
})

test('ORCH_007_every_progress_case_yields_exactly_one_action', () => {
  // Totality, asserted as a table. A new `JobProgress` case with no branch would
  // make `recoveryAction` throw here rather than silently pick a neighbour.
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

  assert.deepEqual(
    table.map(([name, value]) => [name, actionAt('h1', jobAt(value))]),
    table.map(([name, , expected]) => [name, expected]),
  )
})

// ── ORCH-006 through the fold ───────────────────────────────────────────────

const orchestratorFact = {
  created: fact('ManagerJobCreated', {
    ManagerJobId: JOB,
    ManagerSessionId: MANAGER,
    ManagerAgent: 'fast-manager',
    WorktreeIdentity: worktreeIdentity('wt_1'),
    WorktreePath: worktreePath('/tmp/wt1'),
    TargetRef: targetRef('refs/heads/main'),
    TargetBranchFrozen: 'refs/heads/main',
  }),
  candidate: fact('CandidateReady', {
    ManagerJobId: JOB,
    CandidateCommit: commitHash('c1'),
    PreRebaseReviewBarrierId: reviewBarrierId('bar_1'),
  }),
  published: fact('Published', {
    ManagerJobId: JOB,
    CandidateCommit: commitHash('c1'),
    ResultingTargetHead: commitHash('r1'),
  }),
}

const foldFacts = (facts) =>
  fold.apply(
    fold.empty,
    facts.map((value, index) => envelope({ seq: index + 1, stream: stream.session(MANAGER), fact: value })),
  )

test('ORCH_006_the_journal_replays_into_the_latest_progress_only', () => {
  const folded = foldFacts([orchestratorFact.created, orchestratorFact.candidate, orchestratorFact.published])
  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))

  const jobs = fold.orchestrator(folded.value)
  const job = orchestratorProjection.tryFind(JOB, jobs)

  // One value, not an accumulation of five optional fields. That is what makes
  // ORCH-007 a match rather than a ranking.
  assert.equal(orchestratorProjection.progressOf(job), 'Published')
  assert.equal(orchestratorProjection.activeJobs(jobs).length, 0)
})

test('ORCH_006_a_progress_fact_before_its_create_is_dropped_not_promoted', () => {
  // No job exists yet, so there is nothing to record against. Inventing one would
  // give it no worktree and no Manager.
  const folded = foldFacts([orchestratorFact.candidate])
  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))

  assert.equal(isSome(orchestratorProjection.tryFind(JOB, fold.orchestrator(folded.value))), false)
})

test('ORCH_006_a_replayed_create_does_not_reset_a_job_that_already_made_progress', () => {
  // Restart recovery re-reads the journal from the beginning, and PERSIST-009's
  // durable-effect protocol retries after `CommitUnknown` — so one journal can
  // legitimately carry the same `ManagerJobCreated` twice.
  //
  // An unconditional overwrite would reset `Progress` to `ManagerStarted`, and
  // ORCH-007 would then be handed a PUBLISHED job that looks freshly created:
  // recovery resumes a Manager for work that already landed on the target ref.
  const folded = foldFacts([
    orchestratorFact.created,
    orchestratorFact.candidate,
    orchestratorFact.published,
    orchestratorFact.created,
  ])

  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  const jobs = fold.orchestrator(folded.value)

  assert.equal(orchestratorProjection.progressOf(orchestratorProjection.tryFind(JOB, jobs)), 'Published')
  assert.equal(orchestratorProjection.activeJobs(jobs).length, 0)
  assert.equal(actionAt('r1', orchestratorProjection.tryFind(JOB, jobs)), 'CleanUp')
})

test('ORCH_003_a_second_create_for_one_job_id_cannot_change_its_manager_or_worktree', () => {
  // The identity is fixed for the job's whole life, so a second create has
  // nothing new to say — including when it disagrees.
  const first = created()
  const again = orchestratorProjection.createJob(
    createPayload({ ManagerAgent: 'deep-manager', WorktreeIdentity: worktreeIdentity('wt_other') }),
    first,
  )

  const job = orchestratorProjection.tryFind(JOB, again)
  assert.deepEqual(
    { agent: job.ManagerAgent, worktree: idValue.worktreeIdentity(job.WorktreeIdentity) },
    { agent: 'fast-manager', worktree: 'wt_1' },
  )
  assert.equal(orchestratorProjection.activeJobs(again).length, 1)
})

// ── PERSIST-009: typed worktree durable effect ──────────────────────────────

const WT = worktreeIdentity('manager/job_1')
const WT_PATH = worktreePath('/tmp/wt1')

const worktreeFact = {
  requested: fact('WorktreeCreateRequested', {
    ManagerJobId: JOB,
    WorktreeIdentity: WT,
    WorktreePath: WT_PATH,
  }),
  created: fact('WorktreeCreated', {
    ManagerJobId: JOB,
    WorktreeIdentity: WT,
    WorktreePath: WT_PATH,
  }),
}

test('PERSIST_009_worktree_request_then_created_marks_identity_created', () => {
  const folded = foldFacts([worktreeFact.requested, worktreeFact.created])
  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))

  const orch = fold.orchestrator(folded.value)
  assert.equal(orchestratorProjection.worktreeEffectOf(WT, orch), 'Created')
})

test('PERSIST_009_duplicate_request_after_created_does_not_regress_to_requested', () => {
  // CommitUnknown retry may rewrite Requested after Accept. Fold must refuse
  // Accepted → Requested regression (PERSIST-009).
  const folded = foldFacts([worktreeFact.requested, worktreeFact.created, worktreeFact.requested])
  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))

  const orch = fold.orchestrator(folded.value)
  assert.equal(orchestratorProjection.worktreeEffectOf(WT, orch), 'Created')
})

test('PERSIST_009_duplicate_created_is_idempotent', () => {
  const folded = foldFacts([worktreeFact.requested, worktreeFact.created, worktreeFact.created])
  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))

  const orch = fold.orchestrator(folded.value)
  assert.equal(orchestratorProjection.worktreeEffectOf(WT, orch), 'Created')
})

test('PERSIST_009_request_alone_is_not_created', () => {
  const folded = foldFacts([worktreeFact.requested])
  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))

  const orch = fold.orchestrator(folded.value)
  assert.equal(orchestratorProjection.worktreeEffectOf(WT, orch), 'Requested')
})

test('PERSIST_009_direct_request_accept_helpers_match_fold', () => {
  let proj = orchestratorProjection.empty
  proj = orchestratorProjection.requestWorktree(WT, WT_PATH, JOB, proj)
  assert.equal(orchestratorProjection.worktreeEffectOf(WT, proj), 'Requested')

  proj = orchestratorProjection.acceptWorktree(WT, WT_PATH, JOB, proj)
  assert.equal(orchestratorProjection.worktreeEffectOf(WT, proj), 'Created')

  // Second request after accept: no-op.
  proj = orchestratorProjection.requestWorktree(WT, WT_PATH, JOB, proj)
  assert.equal(orchestratorProjection.worktreeEffectOf(WT, proj), 'Created')
})
