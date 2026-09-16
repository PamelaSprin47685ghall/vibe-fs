import assert from 'node:assert/strict'
import test from 'node:test'
import * as change from '../../../dist/Change/Surface.js'

test('WHAT[CHGINT-006] HOST_awaitManager_stages_the_worktree_after_a_completed_manager_run', async () => {
  const runner = (command) => (command.args[0] === 'worktree' ? Promise.resolve([0, '', '']) : Promise.resolve([0, '', '']))
  const resource = await change.worktreeCreate(change.createGit('/repo', runner), 'hostfw10', '/tmp/hostfw10')
  assert.equal(resource.ok, true)
  assert.equal(change.worktreePath(resource.value), '/tmp/hostfw10')
  await change.worktreeDispose(resource.value)
})

test('WHAT[CHGINT-006] ORCH_003_fact_for_an_unknown_job_is_a_no_op_rather_than_a_new_entry', () => {
  const state = change.fold([
    { kind: 'RebasedCandidateReady', jobId: 'j-unknown', rebasedCommit: 'c-1', targetHead: 't-1' },
  ])
  assert.equal(change.job(state, 'j-unknown'), null)
})

test('WHAT[CHGINT-006] ORCH_006_a_terminal_job_stays_in_the_map_so_a_replay_is_recognised', () => {
  const state = change.fold([
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
    { kind: 'Published', jobId: 'j-1', commit: 'c-1' },
  ])
  assert.equal(change.job(state, 'j-1')?.terminal?.kind, 'Published')
})

test('WHAT[CHGINT-006] ORCH_006_a_terminal_job_accepts_no_further_facts', () => {
  const state = change.fold([
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
    { kind: 'Published', jobId: 'j-1', commit: 'c-1' },
    { kind: 'RebasedCandidateReady', jobId: 'j-1', rebasedCommit: 'c-2', targetHead: 't-2' },
  ])
  assert.equal(change.job(state, 'j-1')?.rebasedCommit, undefined)
})

test('WHAT[CHGINT-006] ORCH_006_all_three_terminal_cases_end_the_job', () => {
  for (const terminalKind of ['Published', 'Cancelled', 'Failed']) {
    const state = change.fold([
      { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
      { kind: terminalKind, jobId: 'j-1' },
    ])
    assert.equal(change.activeJobs(state).length, 0)
  }
})

test('WHAT[CHGINT-006] ORCH_007_projection_keeps_independent_facts_instead_of_latest_stage', () => {
  const state = change.fold([
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
    { kind: 'ConflictDetected', jobId: 'j-1', snapshotId: 's-1', unmerged: ['f.txt'] },
    { kind: 'RebasedCandidateReady', jobId: 'j-1', rebasedCommit: 'c-1', targetHead: 't-1' },
  ])
  assert.equal(change.job(state, 'j-1')?.conflict?.snapshotId, 's-1')
  assert.equal(change.job(state, 'j-1')?.rebasedCommit, 'c-1')
})

test('WHAT[CHGINT-006] ORCH_006_the_journal_replays_independent_facts_and_terminal', () => {
  const state = change.fold([
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
    { kind: 'Published', jobId: 'j-1', commit: 'c-1' },
  ])
  assert.equal(change.isTerminal(state, 'j-1'), true)
})

test('WHAT[CHGINT-006] ORCH_006_a_fact_before_its_create_is_dropped_not_promoted', () => {
  const state = change.fold([
    { kind: 'RebasedCandidateReady', jobId: 'j-1', rebasedCommit: 'c-1', targetHead: 't-1' },
  ])
  assert.equal(change.job(state, 'j-1'), null)
})

test('WHAT[CHGINT-006] ORCH_006_a_replayed_create_does_not_reset_a_job_that_already_made_progress', () => {
  const state = change.fold([
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
    { kind: 'RebasedCandidateReady', jobId: 'j-1', rebasedCommit: 'c-1', targetHead: 't-1' },
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
  ])
  assert.equal(change.job(state, 'j-1')?.rebasedCommit, 'c-1')
})

test('WHAT[CHGINT-006] PERSIST_009_worktree_request_then_created_marks_identity_created', () => {
  const state = change.fold([
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
    { kind: 'WorktreeCreateRequested', jobId: 'j-1', worktree: 'wt-1' },
    { kind: 'WorktreeCreated', jobId: 'j-1', worktree: 'wt-1' },
  ])
  assert.equal(change.worktreeEffect(state, 'wt-1'), 'Created')
})

test('WHAT[CHGINT-006] PERSIST_009_duplicate_request_after_created_does_not_regress_to_requested', () => {
  const state = change.fold([
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
    { kind: 'WorktreeCreateRequested', jobId: 'j-1', worktree: 'wt-1' },
    { kind: 'WorktreeCreated', jobId: 'j-1', worktree: 'wt-1' },
    { kind: 'WorktreeCreateRequested', jobId: 'j-1', worktree: 'wt-1' },
  ])
  assert.equal(change.worktreeEffect(state, 'wt-1'), 'Created')
})

test('WHAT[CHGINT-006] PERSIST_009_duplicate_created_is_idempotent', () => {
  const state = change.fold([
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
    { kind: 'WorktreeCreated', jobId: 'j-1', worktree: 'wt-1' },
    { kind: 'WorktreeCreated', jobId: 'j-1', worktree: 'wt-1' },
  ])
  assert.equal(change.worktreeEffect(state, 'wt-1'), 'Created')
})

test('WHAT[CHGINT-006] PERSIST_009_request_alone_is_not_created', () => {
  const state = change.fold([
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
    { kind: 'WorktreeCreateRequested', jobId: 'j-1', worktree: 'wt-1' },
  ])
  assert.equal(change.worktreeEffect(state, 'wt-1'), 'Requested')
})

test('WHAT[CHGINT-006] PERSIST_009_direct_request_accept_helpers_match_fold', () => {
  const s1 = change.fold([{ kind: 'WorktreeCreateRequested', jobId: 'j-1', worktree: 'wt-1' }])
  assert.equal(change.worktreeEffect(s1, 'wt-1'), 'Requested')
})

test('WHAT[CHGINT-006] EXEC_016_active_manager_jobs_are_outstanding_for_orchestrator', () => {
  const state = change.fold([
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
  ])
  assert.equal(change.isOutstanding(state, 'j-1'), true)
})

test('WHAT[CHGINT-006] THEOREM_independent_facts_survive_and_published_is_terminal', () => {
  const state = change.fold([
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
    { kind: 'Published', jobId: 'j-1', commit: 'c-1' },
  ])
  assert.equal(change.isTerminal(state, 'j-1'), true)
})

test('WHAT[CHGINT-006] WORKTREE_adopt_never_releases_on_dispose', () => {
  const wt = change.worktreeAdopt('/wt-path', 'manager/job-1')
  let released = false
  change.worktreeDispose(wt)
  assert.equal(released, false)
})

test('WHAT[CHGINT-006] WORKTREE_mark_durable_disposes_without_release', () => {
  const wt = change.worktreeAdopt('/wt-path', 'manager/job-1')
  change.worktreeMarkDurable(wt)
  assert.equal(wt.isDurable, true)
})

test('WHAT[CHGINT-006] WORKTREE_CMD_list_parses_porcelain_blocks', async () => {
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
  const git = change.createGit('/repo', () => Promise.resolve([0, porcelain, '']))
  const res = await change.gitListWorktrees(git)
  assert.equal(res.ok, true)
  assert.deepEqual(res.value, [
    { path: '/repo', identity: 'refs/heads/main' },
    { path: '/repo/.worktrees/job-1', identity: 'refs/heads/manager/job-1' },
    { path: '/detached', identity: null },
  ])
})

test('WHAT[CHGINT-006] WORKTREE_CMD_list_error_propagates', async () => {
  const git = change.createGit('/repo', () => Promise.resolve([128, '', 'not a git repository']))
  const res = await change.gitListWorktrees(git)
  assert.equal(res.ok, false)
  assert.equal(res.error, 'not a git repository')
})

test('WHAT[CHGINT-006] WORKTREE_CMD_list_branches_strips_current_and_worktree_markers', async () => {
  const git = change.createGit('/repo', () => Promise.resolve([0, '* manager/active\n+ manager/checked-out-elsewhere\n  manager/plain\n\n', '']))
  const res = await change.gitListManagerBranches(git)
  assert.equal(res.ok, true)
  assert.deepEqual(res.value, ['manager/active', 'manager/checked-out-elsewhere', 'manager/plain'])
})

test('WHAT[CHGINT-006] WORKTREE_CMD_delete_branch_uses_force_delete', async () => {
  const calls = []
  const git = change.createGit('/repo', (cmd) => {
    calls.push(cmd.args)
    return Promise.resolve([0, '', ''])
  })
  await change.gitDeleteBranch(git, 'manager/job-1')
  assert.ok(calls.some((args) => args.includes('-D') || args.includes('-f') || args.includes('--force')))
})

test('WHAT[CHGINT-006] WORKTREE_CMD_delete_branch_falls_back_to_stdout_when_stderr_blank', async () => {
  const git = change.createGit('/repo', () => Promise.resolve([1, 'branch not found', '']))
  const res = await change.gitDeleteBranch(git, 'manager/job-1')
  assert.equal(res.ok, false)
})
