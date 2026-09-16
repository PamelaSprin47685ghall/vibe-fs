import assert from 'node:assert/strict'
import test from 'node:test'
import * as change from '../../../dist/Change/Surface.js'

test('WHAT[CHGINT-002] GIT_is_dirty_true_only_on_nonempty_porcelain', async () => {
  const gitClean = change.createGit('/repo', () => Promise.resolve([0, '', '']))
  assert.equal(await change.gitIsDirty(gitClean), false)

  const gitDirty = change.createGit('/repo', () => Promise.resolve([0, ' M file.txt\n', '']))
  assert.equal(await change.gitIsDirty(gitDirty), true)
})

test('WHAT[CHGINT-002] GIT_ff_merge_refuses_dirty_target_worktree', async () => {
  const git = change.createGit('/repo', (cmd) => {
    if (cmd.args.includes('status')) return Promise.resolve([0, ' M dirty.txt\n', ''])
    return Promise.resolve([0, '', ''])
  })
  const result = await change.gitFfMerge(git, '/candidate', 'main', 'base-1', 'cand-1')
  assert.equal(result.ok, false)
  assert.match(result.error, /dirty/i)
})

test('WHAT[CHGINT-002] HOST_sweep_failure_aborts_engine_initialization', async () => {
  const host = change.createOrchestratorHost({
    sweepDirty: () => Promise.resolve({ ok: false, error: 'dirty worktree' }),
  })
  const res = await change.hostInitEngine(host)
  assert.equal(res.ok, false)
  assert.match(res.error, /dirty worktree/i)
})

test('WHAT[CHGINT-002] HOST_ForkManagerJob_surfaces_the_engine_verdict_error', async () => {
  const host = change.createOrchestratorHost({
    sweepDirty: () => Promise.resolve({ ok: false, error: 'dirty on fork' }),
  })
  const res = await change.hostForkManagerJob(host, 'job-1', 'charge')
  assert.equal(res.ok, false)
  assert.match(res.error, /dirty on fork/i)
})

test('WHAT[CHGINT-002] WORKTREE_CMD_is_dirty_reads_porcelain', async () => {
  const gitClean = change.createGit('/repo', () => Promise.resolve([0, '', '']))
  assert.equal(await change.worktreeIsDirty(gitClean, '/wt'), false)

  const gitDirty = change.createGit('/repo', () => Promise.resolve([0, '?? untracked.txt\n', '']))
  assert.equal(await change.worktreeIsDirty(gitDirty, '/wt'), true)
})
