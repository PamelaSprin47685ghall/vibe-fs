import assert from 'node:assert/strict'
import test from 'node:test'
import * as change from '../../../dist/Change/Surface.js'
import { stepIntegrationScenario } from './support/gate-scope-fixture.mjs'

test('WHAT[CHGINT-008] GIT_freeze_target_branch_reads_symbolic_ref', async () => {
  const git = change.createGit('/repo', () => Promise.resolve([0, 'main\n', '']))
  assert.equal(await change.gitFreezeTargetBranch(git), 'main')
})

test('WHAT[CHGINT-008] GIT_freeze_target_branch_refuses_detached_head', async () => {
  const git = change.createGit('/repo', () => Promise.resolve([1, '', 'fatal: ref HEAD is not a symbolic ref']))
  const res = await change.gitFreezeTargetBranchResult(git)
  assert.equal(res.ok, false)
})

test('WHAT[CHGINT-008] GIT_freeze_target_branch_blank_stdout_is_detached', async () => {
  const git = change.createGit('/repo', () => Promise.resolve([0, '', '']))
  const res = await change.gitFreezeTargetBranchResult(git)
  assert.equal(res.ok, false)
})

test('WHAT[CHGINT-008] GIT_read_head_returns_commit_hash', async () => {
  const git = change.createGit('/repo', () => Promise.resolve([0, 'abcdef123456\n', '']))
  assert.equal(await change.gitReadHead(git), 'abcdef123456')
})

test('WHAT[CHGINT-008] GIT_read_head_empty_stdout_is_missing', async () => {
  const git = change.createGit('/repo', () => Promise.resolve([0, '', '']))
  assert.equal(await change.gitReadHead(git), null)
})

test('WHAT[CHGINT-008] GIT_get_target_head_missing_branch', async () => {
  const git = change.createGit('/repo', () => Promise.resolve([1, '', 'not found']))
  assert.equal(await change.gitGetTargetHead(git, 'missing'), null)
})

test('WHAT[CHGINT-008] GIT_ff_merge_happy_path_advances_to_candidate', async () => {
  const git = change.createGit('/repo', (cmd) => {
    if (cmd.args.includes('status')) return Promise.resolve([0, '', ''])
    if (cmd.args.includes('symbolic-ref')) return Promise.resolve([0, 'main\n', ''])
    if (cmd.args.includes('merge-base')) return Promise.resolve([0, 'base-1\n', ''])
    if (cmd.args.includes('update-ref')) return Promise.resolve([0, '', ''])
    return Promise.resolve([0, 'base-1\n', ''])
  })
  const res = await change.gitFfMerge(git, '/cand', 'main', 'base-1', 'cand-1')
  assert.equal(res.ok, true)
})

test('WHAT[CHGINT-008] GIT_ff_merge_refuses_when_repo_on_wrong_branch', async () => {
  const git = change.createGit('/repo', (cmd) => {
    if (cmd.args.includes('status')) return Promise.resolve([0, '', ''])
    if (cmd.args.includes('symbolic-ref')) return Promise.resolve([0, 'other\n', ''])
    return Promise.resolve([0, '', ''])
  })
  const res = await change.gitFfMerge(git, '/cand', 'main', 'base-1', 'cand-1')
  assert.equal(res.ok, false)
  assert.match(res.error, /wrong branch/i)
})

test('WHAT[CHGINT-008] GIT_ff_merge_refuses_detached_with_placeholder', async () => {
  const git = change.createGit('/repo', (cmd) => {
    if (cmd.args.includes('status')) return Promise.resolve([0, '', ''])
    if (cmd.args.includes('symbolic-ref')) return Promise.resolve([1, '', 'detached'])
    return Promise.resolve([0, '', ''])
  })
  const res = await change.gitFfMerge(git, '/cand', 'main', 'base-1', 'cand-1')
  assert.equal(res.ok, false)
})

test('WHAT[CHGINT-008] GIT_ff_merge_refuses_when_target_moved_since_head_read', async () => {
  const git = change.createGit('/repo', (cmd) => {
    if (cmd.args.includes('status')) return Promise.resolve([0, '', ''])
    if (cmd.args.includes('symbolic-ref')) return Promise.resolve([0, 'main\n', ''])
    if (cmd.args.includes('rev-parse')) return Promise.resolve([0, 'moved-head\n', ''])
    return Promise.resolve([0, '', ''])
  })
  const res = await change.gitFfMerge(git, '/cand', 'main', 'expected-head', 'cand-1')
  assert.equal(res.ok, false)
  assert.match(res.error, /target ref moved/i)
})

test('WHAT[CHGINT-008] GIT_ff_merge_refuses_non_fast_forward_candidate', async () => {
  const git = change.createGit('/repo', (cmd) => {
    if (cmd.args.includes('status')) return Promise.resolve([0, '', ''])
    if (cmd.args.includes('symbolic-ref')) return Promise.resolve([0, 'main\n', ''])
    if (cmd.args.includes('merge-base')) return Promise.resolve([0, 'other-base\n', ''])
    return Promise.resolve([0, 'expected-head\n', ''])
  })
  const res = await change.gitFfMerge(git, '/cand', 'main', 'expected-head', 'cand-1')
  assert.equal(res.ok, false)
  assert.match(res.error, /not a fast-forward/i)
})

test('WHAT[CHGINT-008] GIT_ff_merge_ref_moved_lock_diagnostic_maps_to_cas_error', async () => {
  const git = change.createGit('/repo', (cmd) => {
    if (cmd.args.includes('status')) return Promise.resolve([0, '', ''])
    if (cmd.args.includes('symbolic-ref')) return Promise.resolve([0, 'main\n', ''])
    if (cmd.args.includes('update-ref')) return Promise.resolve([1, '', 'fatal: cannot lock ref: is at other-commit'])
    return Promise.resolve([0, 'expected-head\n', ''])
  })
  const res = await change.gitFfMerge(git, '/cand', 'main', 'expected-head', 'cand-1')
  assert.equal(res.ok, false)
})

test('WHAT[CHGINT-008] GIT_ff_merge_generic_merge_failure_surfaces_message', async () => {
  const git = change.createGit('/repo', (cmd) => {
    if (cmd.args.includes('status')) return Promise.resolve([0, '', ''])
    if (cmd.args.includes('symbolic-ref')) return Promise.resolve([0, 'main\n', ''])
    if (cmd.args.includes('update-ref')) return Promise.resolve([1, '', 'custom update-ref error'])
    return Promise.resolve([0, 'expected-head\n', ''])
  })
  const res = await change.gitFfMerge(git, '/cand', 'main', 'expected-head', 'cand-1')
  assert.equal(res.ok, false)
  assert.match(res.error, /custom update-ref error/i)
})

test('WHAT[CHGINT-008] GIT_ff_merge_empty_candidate_head_is_an_error', async () => {
  const git = change.createGit('/repo', () => Promise.resolve([0, '', '']))
  const res = await change.gitFfMerge(git, '/cand', 'main', 'expected-head', '')
  assert.equal(res.ok, false)
})

test('WHAT[CHGINT-008] GIT_ff_merge_verify_head_mismatch_reports_actual', async () => {
  const git = change.createGit('/repo', (cmd) => {
    if (cmd.args.includes('status')) return Promise.resolve([0, '', ''])
    if (cmd.args.includes('symbolic-ref')) return Promise.resolve([0, 'main\n', ''])
    if (cmd.args.includes('rev-parse')) return Promise.resolve([0, 'actual-head\n', ''])
    return Promise.resolve([0, '', ''])
  })
  const res = await change.gitFfMerge(git, '/cand', 'main', 'expected-head', 'cand-1')
  assert.equal(res.ok, false)
})

test('WHAT[CHGINT-008] GIT_create_with_runner_binds_dot_repo', () => {
  const git = change.createGit('/my-repo', () => Promise.resolve([0, '', '']))
  assert.equal(git.repo, '/my-repo')
})

test('WHAT[CHGINT-008] THEOREM_unreadable_target_head_fails_closed', () => {
  const state = change.fold([
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
    { kind: 'PublishClaimed', jobId: 'j-1', rebasedCommit: 'c-1', expectedHead: 't-1' },
  ])
  assert.equal(change.classifyPublishClaim(state, 'j-1', null), 'UnreadableFailClosed')
})

test('WHAT[CHGINT-008] reentry with candidate moving between read and merge sends zero FF', async () => {
  const result = await stepIntegrationScenario([
    { kind: 'reentry:candidate-moved' },
  ])
  assert.equal(result.timeline.filter((s) => s.startsWith('ff:')).length, 0)
})

test('WHAT[CHGINT-008] reentry with landed mismatch never records Published', async () => {
  const result = await stepIntegrationScenario([
    { kind: 'reentry:landed-mismatch' },
  ])
  assert.notEqual(result.verdict?.kind, 'Published')
})
