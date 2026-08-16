// tests/unit/git/worktree-resource.test.mjs — VERIFY-009 coverage: owned manager worktree.
//
// GitPort is fully faked; Release/DisposeAsync semantics and the WorktreeCommands
// process verbs (porcelain parsing, branch markers) are pinned against a fake runner.

import assert from 'node:assert/strict'
import test from 'node:test'

import { listItems, managerJobId, resultOf, worktreeIdentity, worktreePath } from '../../verification-system/tests/support/domain.mjs'

const {
  WorktreeCommands_create,
  WorktreeCommands_deleteBranch,
  WorktreeCommands_identityOf,
  WorktreeCommands_isDirty,
  WorktreeCommands_list,
  WorktreeCommands_listBranches,
  WorktreeCommands_remove,
  WorktreeResource__get_Identity,
  WorktreeResource__get_Path,
  WorktreeResource__MarkDurable,
  WorktreeResource__Release,
  WorktreeResource,
} = await import('../../../dist/Git/WorktreeResource.js')
const worktreeModule = await import('../../../dist/Git/WorktreeResource.js')
const adopt = Object.entries(worktreeModule).find(([k]) => k.startsWith('WorktreeResource_Adopt_'))?.[1]
const createWorktree = Object.entries(worktreeModule).find(([k]) => k.startsWith('WorktreeResource_Create_'))?.[1]

const resourcePath = (resource) => WorktreeResource__get_Path(resource)
const resourceIdentity = (resource) => WorktreeResource__get_Identity(resource)
const release = (resource) => WorktreeResource__Release(resource)
const markDurable = (resource) => WorktreeResource__MarkDurable(resource)

// ── WorktreeResource lifecycle (fake GitPort) ────────────────────────────────

const fakeGit = (behaviour = {}) => {
  const calls = []
  const respond = (name) => behaviour[name] ?? { tag: 0, fields: [] }
  return {
    calls,
    port: {
      IsDirty: () => Promise.resolve(false),
      CreateWorktree: (jobId, path) => {
        calls.push(['CreateWorktree', jobId.fields[0], path.fields[0]])
        const result = respond('CreateWorktree')
        return Promise.resolve(result.tag === 0 ? { tag: 0, fields: [worktreeIdentity(`manager/${jobId.fields[0]}`)] } : result)
      },
      FreezeTargetBranch: () => Promise.resolve({ tag: 1, fields: ['unused'] }),
      Rebase: () => Promise.resolve({ tag: 0, fields: [] }),
      ConflictedFiles: () => Promise.resolve({ tag: 0, fields: [[]] }),
      FfMerge: () => Promise.resolve({ tag: 1, fields: ['unused'] }),
      RemoveWorktree: (path) => {
        calls.push(['RemoveWorktree', path.fields[0]])
        return Promise.resolve(respond('RemoveWorktree'))
      },
      HasRebaseHead: () => Promise.resolve(false),
      ListWorktrees: () => Promise.resolve({ tag: 0, fields: [[]] }),
      ListManagerBranches: () => Promise.resolve({ tag: 0, fields: [[]] }),
      DeleteBranch: (identity) => {
        calls.push(['DeleteBranch', identity.fields[0]])
        return Promise.resolve(respond('DeleteBranch'))
      },
      ReadHead: () => Promise.resolve({ tag: 1, fields: ['unused'] }),
      GetTargetHead: () => Promise.resolve({ tag: 1, fields: ['unused'] }),
    },
  }
}

const PATH = '/repo/.worktrees/job-9'

test('WHAT[CHGINT-005] WORKTREE_create_returns_owned_resource_and_marks_path_identity', async () => {
  const { port, calls } = fakeGit()
  const created = resultOf(await createWorktree(port, managerJobId('job-9'), worktreePath(PATH)))

  assert.equal(created.ok, true)
  const resource = created.value
  assert.equal(resourcePath(resource).fields[0], PATH)
  assert.equal(resourceIdentity(resource).fields[0], 'manager/job-9')
  assert.deepEqual(calls, [['CreateWorktree', 'job-9', PATH]])
})

test('WHAT[CHGINT-003] WORKTREE_create_propagates_port_error', async () => {
  const { port } = fakeGit({ CreateWorktree: { tag: 1, fields: ['worktree add exploded'] } })
  const created = resultOf(await createWorktree(port, managerJobId('job-9'), worktreePath(PATH)))

  assert.equal(created.ok, false)
  assert.equal(created.error, 'worktree add exploded')
})

test('WHAT[CHGINT-005] WORKTREE_release_removes_worktree_and_branch_once', async () => {
  const { port, calls } = fakeGit()
  const created = resultOf(await createWorktree(port, managerJobId('job-9'), worktreePath(PATH)))
  const resource = created.value

  const first = resultOf(await release(resource))
  const second = resultOf(await release(resource))

  assert.equal(first.ok, true)
  assert.equal(second.ok, true, 'release is idempotent')
  const removals = calls.filter(([name]) => name === 'RemoveWorktree')
  assert.equal(removals.length, 1, 'second release must not re-run physical cleanup')
  assert.deepEqual(calls.filter(([name]) => name === 'DeleteBranch'), [['DeleteBranch', 'manager/job-9']])
})

test('WHAT[CHGINT-005] WORKTREE_release_aggregates_both_failures', async () => {
  const { port } = fakeGit({
    RemoveWorktree: { tag: 1, fields: ['rm failed'] },
    DeleteBranch: { tag: 1, fields: ['branch failed'] },
  })
  const created = resultOf(await createWorktree(port, managerJobId('job-9'), worktreePath(PATH)))
  const result = resultOf(await release(created.value))

  assert.equal(result.ok, false)
  assert.equal(result.error, 'worktree=rm failed; branch=branch failed')
})

test('WHAT[CHGINT-005] WORKTREE_release_reports_single_failure_side', async () => {
  const { port } = fakeGit({ RemoveWorktree: { tag: 1, fields: ['rm failed'] } })
  const created = resultOf(await createWorktree(port, managerJobId('job-9'), worktreePath(PATH)))
  const result = resultOf(await release(created.value))
  assert.equal(result.error, 'worktree=rm failed')

  const { port: port2 } = fakeGit({ DeleteBranch: { tag: 1, fields: ['branch failed'] } })
  const created2 = resultOf(await createWorktree(port2, managerJobId('job-9'), worktreePath(PATH)))
  const result2 = resultOf(await release(created2.value))
  assert.equal(result2.error, 'branch=branch failed')
})

test('WHAT[CHGINT-006] WORKTREE_adopt_never_releases_on_dispose', async () => {
  const { port, calls } = fakeGit()
  const resource = adopt(port, worktreeIdentity('manager/job-9'), worktreePath(PATH))

  assert.equal(resourceIdentity(resource).fields[0], 'manager/job-9')
  await resource["System.IAsyncDisposable.DisposeAsync"]()

  assert.deepEqual(
    calls.filter(([name]) => name === 'RemoveWorktree' || name === 'DeleteBranch'),
    [],
    'an adopted resource must not clean up on dispose (recovery owns it)',
  )
})

test('WHAT[CHGINT-006] WORKTREE_mark_durable_disposes_without_release', async () => {
  const { port, calls } = fakeGit()
  const created = resultOf(await createWorktree(port, managerJobId('job-9'), worktreePath(PATH)))
  const resource = created.value

  markDurable(resource)
  await resource["System.IAsyncDisposable.DisposeAsync"]()

  assert.deepEqual(
    calls.filter(([name]) => name === 'RemoveWorktree' || name === 'DeleteBranch'),
    [],
    'a durable worktree (published) must survive dispose',
  )
})

test('WHAT[CHGINT-005] WORKTREE_unreleased_resource_disposes_by_releasing', async () => {
  const { port, calls } = fakeGit()
  const created = resultOf(await createWorktree(port, managerJobId('job-9'), worktreePath(PATH)))

  await created.value["System.IAsyncDisposable.DisposeAsync"]()

  assert.equal(
    calls.filter(([name]) => name === 'RemoveWorktree').length,
    1,
    'an unreleased worktree is cleaned up on dispose',
  )
})

// ── WorktreeCommands (fake runner) ───────────────────────────────────────────

const fakeRunner = (answers) => {
  const calls = []
  const runner = (command) => {
    const args = listItems(command.Arguments)
    calls.push({ args, cwd: command.WorkingDirectory })
    const key = args.join(' ')
    for (const [prefix, response] of answers) {
      if (key.startsWith(prefix)) return Promise.resolve(response)
    }
    return Promise.resolve([0, '', ''])
  }
  return { runner, calls }
}

test('WHAT[CHGINT-009] WORKTREE_CMD_identity_of_is_manager_slash_job', () => {
  assert.equal(WorktreeCommands_identityOf(managerJobId('job-7')).fields[0], 'manager/job-7')
})

test('WHAT[CHGINT-003] WORKTREE_CMD_create_returns_identity_on_success', async () => {
  const { runner, calls } = fakeRunner([])
  const result = resultOf(await WorktreeCommands_create(runner, '/repo', managerJobId('job-3'), worktreePath(PATH)))

  assert.equal(result.ok, true)
  assert.equal(result.value.fields[0], 'manager/job-3')
  assert.deepEqual(calls[0].args, ['worktree', 'add', PATH, '-b', 'manager/job-3'])
  assert.equal(calls[0].cwd, '/repo')
})

test('WHAT[CHGINT-003] WORKTREE_CMD_create_surfaces_stderr_on_failure', async () => {
  const { runner } = fakeRunner([['worktree add', [1, '', 'already exists']]])
  const result = resultOf(await WorktreeCommands_create(runner, '/repo', managerJobId('job-3'), worktreePath(PATH)))
  assert.equal(result.error, 'already exists')
})

test('WHAT[CHGINT-005] WORKTREE_CMD_remove_force_flag_and_no_cwd', async () => {
  const { runner, calls } = fakeRunner([])
  const result = resultOf(await WorktreeCommands_remove(runner, worktreePath(PATH)))

  assert.equal(result.ok, true)
  assert.deepEqual(calls[0].args, ['worktree', 'remove', '--force', PATH])
  assert.equal(calls[0].cwd, undefined)
})

test('WHAT[CHGINT-002] WORKTREE_CMD_is_dirty_reads_porcelain', async () => {
  const { runner: dirty } = fakeRunner([['status --porcelain', [0, ' M x.fs\n', '']]])
  assert.equal(await WorktreeCommands_isDirty(dirty, worktreePath(PATH)), true)

  const { runner: clean } = fakeRunner([['status --porcelain', [0, '\n', '']]])
  assert.equal(await WorktreeCommands_isDirty(clean, worktreePath(PATH)), false)
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
  const { runner } = fakeRunner([['worktree list --porcelain', [0, porcelain, '']]])

  const result = resultOf(await WorktreeCommands_list(runner, '/repo'))

  assert.equal(result.ok, true)
  const entries = listItems(result.value).map(([path, branchOpt]) => [
    path.fields[0],
    branchOpt === undefined ? undefined : branchOpt.fields[0],
  ])
  assert.deepEqual(entries, [
    ['/repo', 'refs/heads/main'],
    ['/repo/.worktrees/job-1', 'refs/heads/manager/job-1'],
    ['/detached', undefined],
  ])
})

test('WHAT[CHGINT-006] WORKTREE_CMD_list_error_propagates', async () => {
  const { runner } = fakeRunner([['worktree list --porcelain', [128, '', 'not a git repository']]])
  const result = resultOf(await WorktreeCommands_list(runner, '/repo'))
  assert.equal(result.ok, false)
  assert.equal(result.error, 'not a git repository')
})

test('WHAT[CHGINT-006] WORKTREE_CMD_list_branches_strips_current_and_worktree_markers', async () => {
  const { runner } = fakeRunner([
    ['branch --list manager/*', [0, '* manager/active\n+ manager/checked-out-elsewhere\n  manager/plain\n\n', '']],
  ])

  const result = resultOf(await WorktreeCommands_listBranches(runner, '/repo'))

  assert.equal(result.ok, true)
  assert.deepEqual(
    listItems(result.value).map((identity) => identity.fields[0]),
    ['manager/active', 'manager/checked-out-elsewhere', 'manager/plain'],
  )
})

test('WHAT[CHGINT-006] WORKTREE_CMD_delete_branch_uses_force_delete', async () => {
  const { runner, calls } = fakeRunner([])
  const result = resultOf(await WorktreeCommands_deleteBranch(runner, '/repo', worktreeIdentity('manager/job-3')))

  assert.equal(result.ok, true)
  assert.deepEqual(calls[0].args, ['branch', '-D', 'manager/job-3'])
})

test('WHAT[CHGINT-006] WORKTREE_CMD_delete_branch_falls_back_to_stdout_when_stderr_blank', async () => {
  const { runner } = fakeRunner([['branch -D', [1, 'branch not found', '']]])
  const result = resultOf(await WorktreeCommands_deleteBranch(runner, '/repo', worktreeIdentity('manager/job-3')))
  assert.equal(result.error, 'branch not found')
})
