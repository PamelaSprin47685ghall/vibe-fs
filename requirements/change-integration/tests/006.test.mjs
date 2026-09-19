import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");

const change = await import('../../../dist/Change/Surface.js')
const job = (id, path = `/tmp/${id}`) => ({
  jobId: id,
  managerSessionId: `ses-${id}`,
  managerAgent: 'manager',
  byname: id,
  worktreeIdentity: `manager/${id}`,
  worktreePath: path,
  targetRef: 'refs/heads/main',
  targetBranchFrozen: 'refs/heads/main',
})

test('WHAT[change-integration-006] HOST_awaitManager_stages_the_worktree_after_a_completed_manager_run', async () => {
  const runner = (command) => command.args[0] === 'worktree' ? Promise.resolve([0, '', '']) : Promise.resolve([0, '', ''])
  const resource = await change.worktreeCreate(change.createGit('/repo', runner), 'hostfw10', '/tmp/hostfw10')
  assert.equal(resource.ok, true)
  assert.equal(change.worktreePath(resource.value), '/tmp/hostfw10')
  await change.worktreeDispose(resource.value)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");

const change = await import('../../../dist/Change/Surface.js')
const JOB = 'job_1'
const MANAGER = 'ses_m'
const payload = (overrides = {}) => ({
  jobId: JOB,
  managerSessionId: MANAGER,
  managerAgent: 'manager',
  byname: 'Road',
  worktreeIdentity: 'wt_1',
  worktreePath: '/tmp/wt1',
  targetRef: 'refs/heads/main',
  targetBranchFrozen: 'refs/heads/main',
  ...overrides,
})
const created = (overrides) => change.createJob(change.empty(), payload(overrides))
const fact = {
  candidateReady: (candidateCommit = 'c1', workspaceSnapshotId = 'snapshot-1', qualityCertificateId = 'certificate-1') =>
    change.fact('CandidateReady', { candidateCommit, workspaceSnapshotId, qualityCertificateId }),
  conflictDetected: (conflictFiles = ['a.fs', 'b.fs']) =>
    change.fact('ConflictDetected', {
      candidateCommit: 'c1',
      targetHeadSnapshot: 'h1',
      workspaceSnapshotId: 'snapshot-conflict',
      conflictFiles,
      diagnosticsDigest: 'digest',
    }),
  rebased: (targetHeadSnapshot = 'h1', workspaceSnapshotId = 'snapshot-rebased') =>
    change.fact('RebasedCandidateReady', {
      rebasedCommit: 'r1',
      targetHeadSnapshot,
      workspaceSnapshotId,
    }),
  publishClaimed: (
    workspaceSnapshotId = 'snapshot-rebased',
    qualityCertificateId = 'certificate-1',
    authorityRevision = 'authority-1',
  ) =>
    change.fact('PublishClaimed', {
      targetRef: 'refs/heads/main',
      rebasedCommit: 'r1',
      expectedHead: 'h1',
      workspaceSnapshotId,
      qualityCertificateId,
      authorityRevision,
    }),
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
const createdEvent = {
  kind: 'ManagerJobCreated',
  payload: payload(),
}
const candidateEvent = {
  kind: 'CandidateReady',
  payload: {
    jobId: JOB,
    candidateCommit: 'c1',
    workspaceSnapshotId: 'snapshot-1',
    qualityCertificateId: 'certificate-1',
  },
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
const worktreeRequested = { kind: 'WorktreeCreateRequested', payload: { jobId: JOB, worktreeIdentity: 'manager/job_1', worktreePath: '/tmp/wt1' } }
const worktreeCreated = { kind: 'WorktreeCreated', payload: { jobId: JOB, worktreeIdentity: 'manager/job_1', worktreePath: '/tmp/wt1' } }

test('WHAT[change-integration-006] ORCH_003_fact_for_an_unknown_job_is_a_no_op_rather_than_a_new_entry', () => {
  const projection = change.recordFact(created(), 'never', fact.candidateReady())
  assert.equal(change.activeJobs(projection).length, 1)
  assert.equal(change.find(projection, 'never'), null)
})
test('WHAT[change-integration-006] ORCH_006_a_terminal_job_stays_in_the_map_so_a_replay_is_recognised', () => {
  const published = change.recordFact(created(), JOB, fact.published())
  assert.notEqual(change.find(published, JOB), null)
  assert.equal(change.activeJobs(published).length, 0)
  const replayed = change.recordFact(published, JOB, fact.published())
  assert.deepEqual(change.find(replayed, JOB).facts, ['Published'])
})
test('WHAT[change-integration-006] ORCH_006_a_terminal_job_accepts_no_further_facts', () => {
  const published = change.recordFact(created(), JOB, fact.published())
  for (const later of [fact.candidateReady('c9'), fact.rebased(), fact.failed('late')]) {
    const after = change.recordFact(published, JOB, later)
    assert.deepEqual(change.find(after, JOB).facts, ['Published'])
  }
})
test('WHAT[change-integration-006] ORCH_006_all_three_terminal_cases_end_the_job', () => {
  for (const terminal of [fact.published(), fact.failed(), fact.abandoned()]) {
    const projection = change.recordFact(created(), JOB, terminal)
    assert.equal(change.activeJobs(projection).length, 0)
  }
})
test('WHAT[change-integration-006] ORCH_007_projection_keeps_independent_facts_instead_of_latest_stage', () => {
  const candidate = jobAt(fact.candidateReady())
  const conflicted = jobAt(fact.conflictDetected(), candidate.projection)
  assert.deepEqual(conflicted.job.facts, ['CandidateReady', 'ConflictDetected'])
})
test('WHAT[change-integration-006] ORCH_006_the_journal_replays_independent_facts_and_terminal', () => {
  const projection = foldProjection([createdEvent, candidateEvent, publishedEvent])
  assert.deepEqual(change.find(projection, JOB).facts, ['CandidateReady', 'Published'])
  assert.equal(change.activeJobs(projection).length, 0)
})
test('WHAT[change-integration-006] ORCH_006_a_fact_before_its_create_is_dropped_not_promoted', () => {
  const projection = foldProjection([candidateEvent])
  assert.equal(change.find(projection, JOB), null)
})
test('WHAT[change-integration-006] ORCH_006_a_replayed_create_does_not_reset_a_job_that_already_made_progress', () => {
  const projection = foldProjection([createdEvent, candidateEvent, publishedEvent, createdEvent])
  assert.deepEqual(change.find(projection, JOB).facts, ['CandidateReady', 'Published'])
  assert.equal(change.activeJobs(projection).length, 0)
})
test('WHAT[change-integration-006] PERSIST_009_worktree_request_then_created_marks_identity_created', () => {
  const projection = foldProjection([worktreeRequested, worktreeCreated])
  assert.equal(change.worktreeEffect(projection, 'manager/job_1'), 'Created')
})
test('WHAT[change-integration-006] PERSIST_009_duplicate_request_after_created_does_not_regress_to_requested', () => {
  const projection = foldProjection([worktreeRequested, worktreeCreated, worktreeRequested])
  assert.equal(change.worktreeEffect(projection, 'manager/job_1'), 'Created')
})
test('WHAT[change-integration-006] PERSIST_009_duplicate_created_is_idempotent', () => {
  const projection = foldProjection([worktreeRequested, worktreeCreated, worktreeCreated])
  assert.equal(change.worktreeEffect(projection, 'manager/job_1'), 'Created')
})
test('WHAT[change-integration-006] PERSIST_009_request_alone_is_not_created', () => {
  const projection = foldProjection([worktreeRequested])
  assert.equal(change.worktreeEffect(projection, 'manager/job_1'), 'Requested')
})
test('WHAT[change-integration-006] PERSIST_009_direct_request_accept_helpers_match_fold', () => {
  let projection = change.empty()
  projection = change.requestWorktree(projection, 'manager/job_1', '/tmp/wt1', JOB)
  assert.equal(change.worktreeEffect(projection, 'manager/job_1'), 'Requested')
  projection = change.acceptWorktree(projection, 'manager/job_1', '/tmp/wt1', JOB)
  assert.equal(change.worktreeEffect(projection, 'manager/job_1'), 'Created')
  projection = change.requestWorktree(projection, 'manager/job_1', '/tmp/wt1', JOB)
  assert.equal(change.worktreeEffect(projection, 'manager/job_1'), 'Created')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");

const change = await import('../../../dist/Change/Surface.js')

test('WHAT[change-integration-006] EXEC_016_active_manager_jobs_are_outstanding_for_orchestrator', () => {
  let jobs = change.createJob(change.empty(), {
    jobId: 'job_1',
    managerSessionId: 'ses_mgr',
    managerAgent: 'manager',
    byname: 'Road',
    worktreeIdentity: 'manager/job_1',
    worktreePath: '/tmp/wt',
    targetRef: 'refs/heads/main',
    targetBranchFrozen: 'main',
  })
  assert.equal(change.activeJobs(jobs).length, 1)

  jobs = change.recordFact(
    jobs,
    'job_1',
    change.fact('Published', { candidateCommit: 'c1', resultingTargetHead: 'r1' }),
  )
  assert.equal(change.activeJobs(jobs).length, 0)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");

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
const candidateEvent = (
  jobId,
  candidateCommit = 'c1',
  workspaceSnapshotId = 'snapshot-1',
  qualityCertificateId = 'certificate-1',
) => ({
  kind: 'CandidateReady',
  payload: { jobId, candidateCommit, workspaceSnapshotId, qualityCertificateId },
})
const conflictEvent = (jobId, { candidateCommit = 'c1', targetHeadSnapshot = 'h1', conflictFiles = ['publish_proof.txt'] } = {}) => ({
  kind: 'ConflictDetected',
  payload: {
    jobId,
    candidateCommit,
    targetHeadSnapshot,
    workspaceSnapshotId: 'snapshot-conflict',
    conflictFiles,
    diagnosticsDigest: 'conflict-digest',
  },
})
const rebasedEvent = (
  jobId,
  { rebasedCommit = 'r1', targetHeadSnapshot = 'h1', workspaceSnapshotId = 'snapshot-rebased' } = {},
) => ({
  kind: 'RebasedCandidateReady',
  payload: { jobId, rebasedCommit, targetHeadSnapshot, workspaceSnapshotId },
})
const publishClaimedEvent = (jobId, expectedHead = 'h1') => ({
  kind: 'PublishClaimed',
  payload: {
    jobId,
    targetRef: 'refs/heads/main',
    rebasedCommit: 'r1',
    expectedHead,
    workspaceSnapshotId: 'snapshot-rebased',
    qualityCertificateId: 'certificate-1',
    authorityRevision: 'authority-1',
  },
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

test('WHAT[change-integration-006] THEOREM_independent_facts_survive_and_published_is_terminal', () => {
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
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { assertOpaque } = await import("../../verification-system/tests/support/js-contract.mjs");

const change = await import('../../../dist/Change/Surface.js')
const PATH = '/repo/.worktrees/job-9'
const fakeRunner = (answers = []) => {
  const calls = []
  const runner = (command) => {
    const args = command.args
    calls.push({ args, cwd: command.workingDirectory })
    const key = args.join(' ')
    for (const [prefix, response] of answers) {
      if (key.startsWith(prefix)) return Promise.resolve(response)
    }
    return Promise.resolve([0, '', ''])
  }
  return { runner, calls }
}
const fakeGit = (answers = []) => {
  const fake = fakeRunner(answers)
  return { ...fake, git: change.createGit('/repo', fake.runner) }
}
const valueOf = async (promise) => {
  const result = await promise
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.value
}

test('WHAT[change-integration-006] WORKTREE_adopt_never_releases_on_dispose', async () => {
  const { git, calls } = fakeGit()
  const resource = change.worktreeAdopt(git, 'manager/job-9', PATH)

  assert.equal(change.worktreeIdentity(resource), 'manager/job-9')
  await change.worktreeDispose(resource)
  assert.deepEqual(
    calls.filter(({ args }) => args[1] === 'remove' || args[0] === 'branch'),
    [],
    'an adopted resource must not clean up on dispose (recovery owns it)',
  )
})
test('WHAT[change-integration-006] WORKTREE_mark_durable_disposes_without_release', async () => {
  const { git, calls } = fakeGit()
  const resource = await valueOf(change.worktreeCreate(git, 'job-9', PATH))
  change.worktreeMarkDurable(resource)
  await change.worktreeDispose(resource)
  assert.deepEqual(
    calls.filter(({ args }) => args[1] === 'remove' || args[0] === 'branch'),
    [],
    'a durable worktree (published) must survive dispose',
  )
})
test('WHAT[change-integration-006] WORKTREE_CMD_list_parses_porcelain_blocks', async () => {
  const porcelain = [
    'worktree /repo',
    'HEAD 0123456789abcdef',
    'branch refs/heads/main',
    '',
    'worktree /repo/.worktrees/job-1',
    'HEAD aabbccddeeff',
    'branch refs/heads/manager/job-1',
    '',
    'worktree /detached',
    'HEAD feedface',
    'detached',
    '',
  ].join('\n')
  const fake = fakeGit([['worktree list --porcelain', [0, porcelain, '']]])
  const result = await change.gitListWorktrees(fake.git)
  assert.equal(result.ok, true)
  assert.deepEqual(result.value, [
    { path: '/repo', identity: 'refs/heads/main' },
    { path: '/repo/.worktrees/job-1', identity: 'refs/heads/manager/job-1' },
    { path: '/detached', identity: null },
  ])
})
test('WHAT[change-integration-006] WORKTREE_CMD_list_error_propagates', async () => {
  const result = await change.gitListWorktrees(fakeGit([['worktree list --porcelain', [128, '', 'not a git repository']]]).git)
  assert.equal(result.ok, false)
  assert.equal(result.error, 'not a git repository')
})
test('WHAT[change-integration-006] WORKTREE_CMD_list_branches_strips_current_and_worktree_markers', async () => {
  const result = await change.gitListManagerBranches(fakeGit([['branch --list manager/*', [0, '* manager/active\n+ manager/checked-out-elsewhere\n  manager/plain\n\n', '']]]).git)
  assert.equal(result.ok, true)
  assert.deepEqual(result.value, ['manager/active', 'manager/checked-out-elsewhere', 'manager/plain'])
})
test('WHAT[change-integration-006] WORKTREE_CMD_delete_branch_uses_force_delete', async () => {
  const fake = fakeGit([])
  const result = await change.gitDeleteBranch(fake.git, 'manager/job-3')
  assert.equal(result.ok, true)
  assert.deepEqual(fake.calls[0].args, ['branch', '-D', 'manager/job-3'])
})
test('WHAT[change-integration-006] WORKTREE_CMD_delete_branch_falls_back_to_stdout_when_stderr_blank', async () => {
  const result = await change.gitDeleteBranch(fakeGit([['branch -D', [1, 'branch not found', '']]]).git, 'manager/job-3')
  assert.equal(result.error, 'branch not found')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const change = await import("../../../dist/Change/Surface.js");

const job = 'job-reentry'
const identity = 'manager/job-reentry'
const path = '/repo/.worktrees/job-reentry'
const decide = (evidence) =>
  change.worktreeReconciliationDecision(job, identity, path, evidence)
const requested = (entries) => ({
  kind: 'RequestedEntries',
  jobId: job,
  path,
  entries,
})

test('WHAT[change-integration-006] PERSIST_009_worktree_requested_created_reentry_is_finite_and_fail_closed', () => {
  // Fresh entry records intent before the ordinary fork CE creates anything.
  assert.deepEqual(decide({ kind: 'NoDurableEffect' }), { kind: 'RequestThenCreate' })

  // Crash after Requested but before the physical effect: a complete empty
  // observation proves that creation is safe on retry.
  assert.deepEqual(decide(requested([])), { kind: 'CreateAfterProvenMissing' })

  // Crash after physical create but before Created: exact identity + path is
  // adopted and the missing receipt is recorded, never recreated.
  assert.deepEqual(
    decide(requested([{ path, identity }])),
    { kind: 'AdoptThenRecordCreated' },
  )

  // Either half of the physical identity/path key conflicting fails closed.
  assert.deepEqual(
    decide(requested([{ path: '/repo/.worktrees/elsewhere', identity }])),
    { kind: 'Reject', reason: 'PhysicalIdentityPathConflict' },
  )
  assert.deepEqual(
    decide(requested([{ path, identity: 'manager/another-job' }])),
    { kind: 'Reject', reason: 'PhysicalIdentityPathConflict' },
  )

  // No guessed success or unsafe retry is available when the physical query fails.
  assert.deepEqual(
    decide({ kind: 'RequestedQueryFailure', jobId: job, path, error: 'git unavailable' }),
    { kind: 'Reject', reason: 'WorktreeQueryFailed' },
  )

  // Created is already the durable receipt: reentry adopts without another query.
  assert.deepEqual(
    decide({ kind: 'CreatedReceipt', jobId: job, path }),
    { kind: 'AdoptCreated' },
  )

  // Durable intent cannot be stolen by another job or redirected to another path;
  // this state is rejected before any physical query is admitted.
  assert.deepEqual(
    decide({ kind: 'RequestedConflict', jobId: 'job-other', path }),
    { kind: 'Reject', reason: 'DurableOwnershipConflict' },
  )
  assert.deepEqual(
    decide({ kind: 'CreatedReceipt', jobId: 'job-other', path }),
    { kind: 'Reject', reason: 'DurableOwnershipConflict' },
  )
})
}
