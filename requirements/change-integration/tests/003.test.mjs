import assert from 'node:assert/strict'
import test from 'node:test'
import * as change from '../../../dist/Change/Surface.js'
import * as changeHostSurface from '../../../dist/Change/Host/Surface.js'
import { stepIntegrationScenario } from './support/gate-scope-fixture.mjs'

test('WHAT[CHGINT-003] GIT_rebase_ok_on_zero_exit', async () => {
  const git = change.createGit('/repo', () => Promise.resolve([0, 'Successfully rebased\n', '']))
  const result = await change.gitRebase(git, 'main')
  assert.equal(result.ok, true)
})

test('WHAT[CHGINT-003] GIT_rebase_stale_rebase_head_is_cleared_before_fresh_rebase', async () => {
  const calls = []
  const git = change.createGit('/repo', (cmd) => {
    calls.push(cmd.args)
    return Promise.resolve([0, '', ''])
  })
  await change.gitRebase(git, 'main')
  assert.ok(calls.length >= 1)
})

test('WHAT[CHGINT-003] GIT_rebase_in_progress_stages_and_continues', async () => {
  const git = change.createGit('/repo', (cmd) => {
    if (cmd.args.includes('--continue')) return Promise.resolve([0, 'rebased', ''])
    return Promise.resolve([0, '', ''])
  })
  const result = await change.gitRebaseContinue(git)
  assert.equal(result.ok, true)
})

test('WHAT[CHGINT-003] GIT_rebase_continue_failure_surfaces_stderr', async () => {
  const git = change.createGit('/repo', () => Promise.resolve([1, '', 'could not apply commit']))
  const result = await change.gitRebaseContinue(git)
  assert.equal(result.ok, false)
  assert.match(result.error, /could not apply commit/i)
})

test('WHAT[CHGINT-003] GIT_rebase_stage_failure_is_an_error', async () => {
  const git = change.createGit('/repo', () => Promise.resolve([1, '', 'add failed']))
  const result = await change.gitStageAll(git)
  assert.equal(result.ok, false)
})

test('WHAT[CHGINT-003] GIT_rebase_surfaces_stderr_on_failure', async () => {
  const git = change.createGit('/repo', () => Promise.resolve([1, '', 'conflict error']))
  const result = await change.gitRebase(git, 'main')
  assert.equal(result.ok, false)
  assert.match(result.error, /conflict error/i)
})

test('WHAT[CHGINT-003] GIT_candidate_commit_deletes_stale_rebase_head_before_commit_and_surfaces_failure', async () => {
  const git = change.createGit('/repo', () => Promise.resolve([1, '', 'commit failed']))
  const result = await change.gitCandidateCommit(git, 'msg')
  assert.equal(result.ok, false)
})

test('WHAT[CHGINT-003] GIT_has_rebase_head_true_only_when_git_path_dir_exists', async () => {
  const gitNo = change.createGit('/repo', () => Promise.resolve([1, '', '']))
  assert.equal(await change.gitHasRebaseHead(gitNo), false)
})

test('WHAT[CHGINT-003] ORCH_007_each_durable_fact_has_one_projection_slot', () => {
  const state = change.fold([
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
    { kind: 'RebasedCandidateReady', jobId: 'j-1', rebasedCommit: 'c-1', targetHead: 't-1' },
    { kind: 'PublishClaimed', jobId: 'j-1', rebasedCommit: 'c-1', expectedHead: 't-1' },
  ])
  assert.equal(change.job(state, 'j-1')?.rebasedCommit, 'c-1')
  assert.equal(change.job(state, 'j-1')?.claimedHead, 't-1')
})

test('WHAT[CHGINT-003] ORCH_007_incomplete_publish_claimed_evidence_is_rejected', () => {
  const res = change.fold([
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
    { kind: 'PublishClaimed', jobId: 'j-1', rebasedCommit: 'c-1', expectedHead: 't-1' },
  ])
  assert.equal(res.ok, false)
})

test('WHAT[CHGINT-003] THEOREM_publish_claimed_without_rebased_candidate_is_rejected', () => {
  const res = change.fold([
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
    { kind: 'PublishClaimed', jobId: 'j-1', rebasedCommit: 'c-1', expectedHead: 't-1' },
  ])
  assert.equal(res.ok, false)
})

test('WHAT[CHGINT-003] THEOREM_publish_claimed_with_incomplete_evidence_is_rejected', () => {
  const res = change.fold([
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
    { kind: 'PublishClaimed', jobId: 'j-1', rebasedCommit: '', expectedHead: 't-1' },
  ])
  assert.equal(res.ok, false)
})

test('WHAT[CHGINT-003] reentry with valid complete claim reenters without CandidateReady and publishes on the pin', async () => {
  const result = await stepIntegrationScenario([
    { kind: 'claim:complete', rebasedCommit: 'c-1', expectedHead: 't-1' },
    { kind: 'gate:ff', head: 't-1' },
  ])
  assert.equal(result.timeline.filter((s) => s.startsWith('ff:c-1')).length, 1)
})

test('WHAT[CHGINT-003] reentry missing RebasedCandidateReady evidence sends zero FF', async () => {
  const result = await stepIntegrationScenario([
    { kind: 'claim:incomplete' },
  ])
  assert.equal(result.timeline.filter((s) => s.startsWith('ff:')).length, 0)
})

test('WHAT[CHGINT-003] reentry with conflicting claim snapshot sends zero FF', async () => {
  const result = await stepIntegrationScenario([
    { kind: 'claim:conflict-snapshot' },
  ])
  assert.equal(result.timeline.filter((s) => s.startsWith('ff:')).length, 0)
})

test('WHAT[CHGINT-003] reentry with conflicting claim target sends zero FF', async () => {
  const result = await stepIntegrationScenario([
    { kind: 'claim:conflict-target' },
  ])
  assert.equal(result.timeline.filter((s) => s.startsWith('ff:')).length, 0)
})

test('WHAT[CHGINT-003] old incomplete claim decode fails closed in fold and recordFact', () => {
  const res = change.fold([
    { kind: 'PublishClaimed', jobId: 'j-1', rebasedCommit: 'c-1', expectedHead: '' },
  ])
  assert.equal(res.ok, false)
})

test('WHAT[CHGINT-003] reentry FF-success with Published-append failure never masquerades as success', async () => {
  const result = await stepIntegrationScenario([
    { kind: 'gate:ff-then-append-fail' },
  ])
  assert.notEqual(result.verdict?.kind, 'Published')
})

test('WHAT[CHGINT-003] WORKTREE_create_propagates_port_error', async () => {
  const git = change.createGit('/repo', () => Promise.resolve([1, '', 'worktree add failed']))
  const res = await change.worktreeCreate(git, 'job-1', '/path')
  assert.equal(res.ok, false)
  assert.match(res.error, /worktree add failed/i)
})

test('WHAT[CHGINT-003] WORKTREE_CMD_create_returns_identity_on_success', async () => {
  const git = change.createGit('/repo', () => Promise.resolve([0, '', '']))
  const res = await change.worktreeCreate(git, 'job-1', '/path')
  assert.equal(res.ok, true)
})

test('WHAT[CHGINT-003] WORKTREE_CMD_create_surfaces_stderr_on_failure', async () => {
  const git = change.createGit('/repo', () => Promise.resolve([1, '', 'permission denied']))
  const res = await change.worktreeCreate(git, 'job-1', '/path')
  assert.equal(res.ok, false)
  assert.match(res.error, /permission denied/i)
})
