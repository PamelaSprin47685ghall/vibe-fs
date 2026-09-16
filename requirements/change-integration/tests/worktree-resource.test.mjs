// CHGINT-002/003/005/006/009 — owned worktree resource and git verbs.

import assert from 'node:assert/strict'
import test from 'node:test'
import { assertOpaque } from '../../verification-system/tests/support/js-contract.mjs'

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

// ── WorktreeResource lifecycle ──────────────────────────────────────────────

test('WHAT[CHGINT-005] WORKTREE_create_returns_owned_resource_and_marks_path_identity', async () => {
  const { git, calls } = fakeGit()
  const resource = await valueOf(change.worktreeCreate(git, 'job-9', PATH))

  assertOpaque(resource, 'owned worktree resource')
  assert.equal(change.worktreePath(resource), PATH)
  assert.equal(change.worktreeIdentity(resource), 'manager/job-9')
  assert.deepEqual(calls, [{ args: ['worktree', 'add', PATH, '-b', 'manager/job-9'], cwd: '/repo' }])
})

test('WHAT[CHGINT-003] WORKTREE_create_propagates_port_error', async () => {
  const { git } = fakeGit([['worktree add', [1, '', 'worktree add exploded']]])
  const result = await change.worktreeCreate(git, 'job-9', PATH)
  assert.equal(result.ok, false)
  assert.equal(result.error, 'worktree add exploded')
})

test('WHAT[CHGINT-005] WORKTREE_release_removes_worktree_and_branch_once', async () => {
  const { git, calls } = fakeGit()
  const resource = await valueOf(change.worktreeCreate(git, 'job-9', PATH))

  const first = await change.worktreeRelease(resource)
  const second = await change.worktreeRelease(resource)
  assert.equal(first.ok, true)
  assert.equal(second.ok, true, 'release is idempotent')
  assert.equal(calls.filter(({ args }) => args[1] === 'remove').length, 1)
  assert.deepEqual(
    calls.filter(({ args }) => args[0] === 'branch'),
    [{ args: ['branch', '-D', 'manager/job-9'], cwd: '/repo' }],
  )
})

test('WHAT[CHGINT-005] WORKTREE_release_aggregates_both_failures', async () => {
  const { git } = fakeGit([
    ['worktree remove', [1, '', 'rm failed']],
    ['branch -D', [1, '', 'branch failed']],
  ])
  const resource = await valueOf(change.worktreeCreate(git, 'job-9', PATH))
  const result = await change.worktreeRelease(resource)
  assert.equal(result.ok, false)
  assert.equal(result.error, 'worktree=rm failed; branch=branch failed')
})

test('WHAT[CHGINT-005] WORKTREE_release_reports_single_failure_side', async () => {
  const first = fakeGit([['worktree remove', [1, '', 'rm failed']]])
  const resource = await valueOf(change.worktreeCreate(first.git, 'job-9', PATH))
  const result = await change.worktreeRelease(resource)
  assert.equal(result.error, 'worktree=rm failed')

  const second = fakeGit([['branch -D', [1, '', 'branch failed']]])
  const adopted = change.worktreeAdopt(second.git, 'manager/job-9', PATH)
  const result2 = await change.worktreeRelease(adopted)
  assert.equal(result2.error, 'branch=branch failed')
})

test('WHAT[CHGINT-006] WORKTREE_adopt_never_releases_on_dispose', async () => {
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

test('WHAT[CHGINT-006] WORKTREE_mark_durable_disposes_without_release', async () => {
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

test('WHAT[CHGINT-005] WORKTREE_unreleased_resource_disposes_by_releasing', async () => {
  const { git, calls } = fakeGit()
  const resource = await valueOf(change.worktreeCreate(git, 'job-9', PATH))
  await change.worktreeDispose(resource)
  assert.equal(calls.filter(({ args }) => args[1] === 'remove').length, 1)
})

// ── WorktreeCommands ─────────────────────────────────────────────────────────

test('WHAT[CHGINT-009] WORKTREE_CMD_identity_of_is_manager_slash_job', () => {
  assert.equal(change.worktreeIdentityOf('job-7'), 'manager/job-7')
})

test('WHAT[CHGINT-003] WORKTREE_CMD_create_returns_identity_on_success', async () => {
  const { git, calls } = fakeGit()
  const result = await change.gitCreateWorktree(git, 'job-3', PATH)
  assert.equal(result.ok, true)
  assert.equal(result.value, 'manager/job-3')
  assert.deepEqual(calls[0].args, ['worktree', 'add', PATH, '-b', 'manager/job-3'])
  assert.equal(calls[0].cwd, '/repo')
})

test('WHAT[CHGINT-003] WORKTREE_CMD_create_surfaces_stderr_on_failure', async () => {
  const { git } = fakeGit([['worktree add', [1, '', 'already exists']]])
  const result = await change.gitCreateWorktree(git, 'job-3', PATH)
  assert.equal(result.error, 'already exists')
})

test('WHAT[CHGINT-005] WORKTREE_CMD_remove_force_flag_and_no_cwd', async () => {
  const fake = fakeRunner([])
  const git = change.createGit('/repo', fake.runner)
  const result = await change.gitRemoveWorktree(git, PATH)
  assert.equal(result.ok, true)
  assert.deepEqual(fake.calls[0].args, ['worktree', 'remove', '--force', PATH])
  assert.equal(fake.calls[0].cwd, undefined)
})

test('WHAT[CHGINT-002] WORKTREE_CMD_is_dirty_reads_porcelain', async () => {
  assert.equal(await change.gitIsDirty(fakeGit([['status --porcelain', [0, ' M x.fs\n', '']]]).git, PATH), true)
  assert.equal(await change.gitIsDirty(fakeGit([['status --porcelain', [0, '\n', '']]]).git, PATH), false)
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
  const fake = fakeGit([['worktree list --porcelain', [0, porcelain, '']]])
  const result = await change.gitListWorktrees(fake.git)
  assert.equal(result.ok, true)
  assert.deepEqual(result.value, [
    { path: '/repo', identity: 'refs/heads/main' },
    { path: '/repo/.worktrees/job-1', identity: 'refs/heads/manager/job-1' },
    { path: '/detached', identity: null },
  ])
})

test('WHAT[CHGINT-006] WORKTREE_CMD_list_error_propagates', async () => {
  const result = await change.gitListWorktrees(fakeGit([['worktree list --porcelain', [128, '', 'not a git repository']]]).git)
  assert.equal(result.ok, false)
  assert.equal(result.error, 'not a git repository')
})

test('WHAT[CHGINT-006] WORKTREE_CMD_list_branches_strips_current_and_worktree_markers', async () => {
  const result = await change.gitListManagerBranches(fakeGit([['branch --list manager/*', [0, '* manager/active\n+ manager/checked-out-elsewhere\n  manager/plain\n\n', '']]]).git)
  assert.equal(result.ok, true)
  assert.deepEqual(result.value, ['manager/active', 'manager/checked-out-elsewhere', 'manager/plain'])
})

test('WHAT[CHGINT-006] WORKTREE_CMD_delete_branch_uses_force_delete', async () => {
  const fake = fakeGit([])
  const result = await change.gitDeleteBranch(fake.git, 'manager/job-3')
  assert.equal(result.ok, true)
  assert.deepEqual(fake.calls[0].args, ['branch', '-D', 'manager/job-3'])
})

test('WHAT[CHGINT-006] WORKTREE_CMD_delete_branch_falls_back_to_stdout_when_stderr_blank', async () => {
  const result = await change.gitDeleteBranch(fakeGit([['branch -D', [1, 'branch not found', '']]]).git, 'manager/job-3')
  assert.equal(result.error, 'branch not found')
})
