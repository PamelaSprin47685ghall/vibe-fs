// tests/unit/git/git-operations.test.mjs — VERIFY-009 coverage: typed Git verbs.
//
// Every verb is exercised against a fake `runner` (Command -> Task<int*string*string>),
// so command construction, output classification and the ff-only CAS ladder are pinned
// without spawning git.

import assert from 'node:assert/strict'
import test from 'node:test'

import { commitHash, listItems, managerJobId, resultOf, targetRef, worktreeIdentity, worktreePath } from '../support/domain.mjs'

const { createWithRepo, createWithRunner } = await import('../../../dist/Infrastructure/Git/GitOperations.js')

/** Fake process host: script answers by (file, args) and records every command.
 *  A response entry of `[tripleA, tripleB, ...]` (array of triples) is consumed in
 *  order — the first match shifts; the last triple stays sticky. */
const fakeRunner = (answers) => {
  const calls = []
  const runner = (command) => {
    const args = listItems(command.Arguments)
    calls.push({ file: command.FileName, args, cwd: command.WorkingDirectory })
    const key = args.join(' ')
    for (const [prefix, response] of answers) {
      if (!key.startsWith(prefix)) continue
      if (Array.isArray(response) && Array.isArray(response[0])) {
        const triple = response.length > 1 ? response.shift() : response[0]
        return Promise.resolve(triple)
      }
      return Promise.resolve(response)
    }
    return Promise.resolve([0, '', ''])
  }
  return { runner, calls }
}

const REPO = '/repo'
const WORKTREE = '/repo/.worktrees/job-1'

// ── FreezeTargetBranch ───────────────────────────────────────────────────────

test('GIT_freeze_target_branch_reads_symbolic_ref', async () => {
  const { runner } = fakeRunner([['symbolic-ref --short HEAD', [0, 'main\n', '']]])
  const port = createWithRepo(REPO, runner)

  const result = resultOf(await port.FreezeTargetBranch())
  assert.equal(result.ok, true)
  assert.equal(result.value.fields[0], 'main')
})

test('GIT_freeze_target_branch_refuses_detached_head', async () => {
  const { runner } = fakeRunner([['symbolic-ref --short HEAD', [128, '', 'fatal: ref HEAD is not a symbolic ref']]])
  const port = createWithRepo(REPO, runner)

  const result = resultOf(await port.FreezeTargetBranch())
  assert.equal(result.ok, false)
  assert.match(result.error, /fatal: ref HEAD is not a symbolic ref/)
})

test('GIT_freeze_target_branch_blank_stdout_is_detached', async () => {
  const { runner } = fakeRunner([['symbolic-ref --short HEAD', [0, '  \n', '']]])
  const port = createWithRepo(REPO, runner)

  const result = resultOf(await port.FreezeTargetBranch())
  assert.equal(result.ok, false)
  assert.match(result.error, /detached/)
})

// ── IsDirty ──────────────────────────────────────────────────────────────────

test('GIT_is_dirty_true_only_on_nonempty_porcelain', async () => {
  const { runner: dirtyRunner } = fakeRunner([['status --porcelain', [0, ' M file.fs\n', '']]])
  assert.equal(await createWithRepo(REPO, dirtyRunner).IsDirty(worktreePath(WORKTREE)), true)

  const { runner: cleanRunner } = fakeRunner([['status --porcelain', [0, '', '']]])
  assert.equal(await createWithRepo(REPO, cleanRunner).IsDirty(worktreePath(WORKTREE)), false)

  const { runner: failingRunner } = fakeRunner([['status --porcelain', [1, '', 'boom']]])
  assert.equal(await createWithRepo(REPO, failingRunner).IsDirty(worktreePath(WORKTREE)), false)
})

// ── Rebase / ConflictedFiles / HasRebaseHead ─────────────────────────────────

test('GIT_rebase_ok_on_zero_exit', async () => {
  const { runner } = fakeRunner([['rebase main', [0, '', '']]])
  const result = resultOf(await createWithRepo(REPO, runner).Rebase(worktreePath(WORKTREE), targetRef('main')))
  assert.equal(result.ok, true)
})

test('GIT_rebase_surfaces_stderr_on_failure', async () => {
  const { runner } = fakeRunner([['rebase main', [1, 'stdout-noise', 'CONFLICT (content)']]])
  const result = resultOf(await createWithRepo(REPO, runner).Rebase(worktreePath(WORKTREE), targetRef('main')))
  assert.equal(result.ok, false)
  assert.equal(result.error, 'CONFLICT (content)')
})

test('GIT_conflicted_files_parses_lines', async () => {
  const { runner } = fakeRunner([['diff --name-only --diff-filter=U', [0, 'a.fs\nb.fs\n', '']]])
  const result = resultOf(await createWithRepo(REPO, runner).ConflictedFiles(worktreePath(WORKTREE)))
  assert.equal(result.ok, true)
  assert.deepEqual(listItems(result.value), ['a.fs', 'b.fs'])
})

test('GIT_conflicted_files_error_propagates', async () => {
  const { runner } = fakeRunner([['diff --name-only --diff-filter=U', [1, '', 'not a repo']]])
  const result = resultOf(await createWithRepo(REPO, runner).ConflictedFiles(worktreePath(WORKTREE)))
  assert.equal(result.ok, false)
  assert.equal(result.error, 'not a repo')
})

test('GIT_has_rebase_head_reflects_exit_code', async () => {
  const { runner: yes } = fakeRunner([['rev-parse --verify REBASE_HEAD', [0, 'abc', '']]])
  assert.equal(await createWithRepo(REPO, yes).HasRebaseHead(worktreePath(WORKTREE)), true)

  const { runner: no } = fakeRunner([['rev-parse --verify REBASE_HEAD', [1, '', '']]])
  assert.equal(await createWithRepo(REPO, no).HasRebaseHead(worktreePath(WORKTREE)), false)
})

// ── ReadHead / GetTargetHead ─────────────────────────────────────────────────

test('GIT_read_head_returns_commit_hash', async () => {
  const { runner } = fakeRunner([['rev-parse HEAD', [0, 'deadbeef\n', '']]])
  const result = resultOf(await createWithRepo(REPO, runner).ReadHead(worktreePath(WORKTREE)))
  assert.equal(result.ok, true)
  assert.equal(result.value.fields[0], 'deadbeef')
})

test('GIT_read_head_empty_stdout_is_missing', async () => {
  const { runner } = fakeRunner([['rev-parse HEAD', [0, '  \n', '']]])
  const result = resultOf(await createWithRepo(REPO, runner).ReadHead(worktreePath(WORKTREE)))
  assert.equal(result.ok, false)
  assert.equal(result.error, 'HEAD is empty')
})

test('GIT_get_target_head_missing_branch', async () => {
  const { runner } = fakeRunner([['rev-parse refs/heads/main', [128, '', '']]])
  const result = resultOf(await createWithRepo(REPO, runner).GetTargetHead(targetRef('main')))
  assert.equal(result.ok, false)
  assert.match(result.error, /target branch not found: main/)
})

// ── FfMerge (the ORCH-005 CAS ladder) ────────────────────────────────────────

const ffAnswers = ({ candidate = 'cafe01', targetHead = 'beef02', branch = 'main' } = {}) => [
  ['symbolic-ref --short HEAD', [0, `${branch}\n`, '']],
  ['rev-parse HEAD', [0, `${candidate}\n`, '']],
  ['rev-parse refs/heads/main', [0, `${targetHead}\n`, '']],
  ['merge-base --is-ancestor', [0, '', '']],
  ['status --porcelain', [0, '', '']],
  ['merge --ff-only', [0, '', '']],
]

test('GIT_ff_merge_happy_path_advances_to_candidate', async () => {
  const { runner, calls } = fakeRunner(ffAnswers())
  const port = createWithRepo(REPO, runner)

  const result = resultOf(
    await port.FfMerge(worktreePath(WORKTREE), targetRef('main'), commitHash('beef02')),
  )

  assert.equal(result.ok, true)
  assert.equal(result.value.fields[0], 'cafe01')
  const sequence = calls.map((call) => call.args[0])
  assert.deepEqual(sequence, [
    'rev-parse', // candidate HEAD in the worktree
    'symbolic-ref', // frozen branch check
    'rev-parse', // current target head
    'merge-base',
    'status', // verifyClean
    'merge',
    'rev-parse', // verifyHead
  ])
})

test('GIT_ff_merge_refuses_when_repo_on_wrong_branch', async () => {
  const { runner } = fakeRunner(ffAnswers({ branch: 'feature' }))
  const port = createWithRepo(REPO, runner)

  const result = resultOf(
    await port.FfMerge(worktreePath(WORKTREE), targetRef('main'), commitHash('beef02')),
  )

  assert.equal(result.ok, false)
  assert.match(result.error, /publish branch mismatch: target repo is on 'feature' but publish is frozen to 'main'/)
})

test('GIT_ff_merge_refuses_detached_with_placeholder', async () => {
  const { runner } = fakeRunner([['rev-parse HEAD', [0, 'cafe01\n', '']], ['symbolic-ref --short HEAD', [128, '', '']]])
  const port = createWithRepo(REPO, runner)

  const result = resultOf(
    await port.FfMerge(worktreePath(WORKTREE), targetRef('main'), commitHash('beef02')),
  )

  assert.equal(result.ok, false)
  assert.match(result.error, /<detached HEAD>/)
})

test('GIT_ff_merge_refuses_when_target_moved_since_head_read', async () => {
  const { runner } = fakeRunner(ffAnswers({ targetHead: 'other9' }))
  const port = createWithRepo(REPO, runner)

  const result = resultOf(
    await port.FfMerge(worktreePath(WORKTREE), targetRef('main'), commitHash('beef02')),
  )

  assert.equal(result.ok, false)
  assert.equal(result.error, 'target ref moved')
})

test('GIT_ff_merge_refuses_non_fast_forward_candidate', async () => {
  const answers = ffAnswers().map(([prefix, response]) =>
    prefix === 'merge-base --is-ancestor' ? [prefix, [1, '', '']] : [prefix, response],
  )
  const { runner } = fakeRunner(answers)
  const port = createWithRepo(REPO, runner)

  const result = resultOf(
    await port.FfMerge(worktreePath(WORKTREE), targetRef('main'), commitHash('beef02')),
  )

  assert.equal(result.ok, false)
  assert.equal(result.error, 'candidate is not a fast-forward of the target branch')
})

test('GIT_ff_merge_refuses_dirty_target_worktree', async () => {
  const answers = ffAnswers().map(([prefix, response]) =>
    prefix === 'status --porcelain' ? [prefix, [0, ' M dirty.fs\n', '']] : [prefix, response],
  )
  const { runner } = fakeRunner(answers)
  const port = createWithRepo(REPO, runner)

  const result = resultOf(
    await port.FfMerge(worktreePath(WORKTREE), targetRef('main'), commitHash('beef02')),
  )

  assert.equal(result.ok, false)
  assert.match(result.error, /target worktree is dirty; refusing ff-only merge/)
})

test('GIT_ff_merge_ref_moved_lock_diagnostic_maps_to_cas_error', async () => {
  const answers = ffAnswers().map(([prefix, response]) =>
    prefix === 'merge --ff-only'
      ? [prefix, [1, '', 'error: cannot lock ref refs/heads/main: is at x but expected y']]
      : [prefix, response],
  )
  const { runner } = fakeRunner(answers)
  const port = createWithRepo(REPO, runner)

  const result = resultOf(
    await port.FfMerge(worktreePath(WORKTREE), targetRef('main'), commitHash('beef02')),
  )

  assert.equal(result.ok, false)
  assert.equal(result.error, 'target ref moved')
})

test('GIT_ff_merge_generic_merge_failure_surfaces_message', async () => {
  const answers = ffAnswers().map(([prefix, response]) =>
    prefix === 'merge --ff-only' ? [prefix, [1, '', 'merge exploded']] : [prefix, response],
  )
  const { runner } = fakeRunner(answers)
  const port = createWithRepo(REPO, runner)

  const result = resultOf(
    await port.FfMerge(worktreePath(WORKTREE), targetRef('main'), commitHash('beef02')),
  )

  assert.equal(result.ok, false)
  assert.equal(result.error, 'merge exploded')
})

test('GIT_ff_merge_empty_candidate_head_is_an_error', async () => {
  const { runner } = fakeRunner([['rev-parse HEAD', [0, ' \n', '']]])
  const port = createWithRepo(REPO, runner)

  const result = resultOf(
    await port.FfMerge(worktreePath(WORKTREE), targetRef('main'), commitHash('beef02')),
  )

  assert.equal(result.ok, false)
  assert.equal(result.error, 'candidate HEAD is empty')
})

test('GIT_ff_merge_verify_head_mismatch_reports_actual', async () => {
  // The candidate read returns cafe01; the post-merge verify read returns wrong00.
  const answers = [
    ['symbolic-ref --short HEAD', [0, 'main\n', '']],
    ['merge-base --is-ancestor', [0, '', '']],
    ['status --porcelain', [0, '', '']],
    ['merge --ff-only', [0, '', '']],
    ['rev-parse refs/heads/main', [0, 'beef02\n', '']],
    ['rev-parse HEAD', [[0, 'cafe01\n', ''], [0, 'wrong00\n', '']]],
  ]
  const { runner } = fakeRunner(answers)
  const port = createWithRepo(REPO, runner)

  const result = resultOf(
    await port.FfMerge(worktreePath(WORKTREE), targetRef('main'), commitHash('beef02')),
  )

  assert.equal(result.ok, false)
  assert.match(result.error, /ff-only merge did not advance HEAD to candidate cafe01 \(got wrong00\)/)
})

test('GIT_create_with_runner_binds_dot_repo', async () => {
  const { runner, calls } = fakeRunner([['symbolic-ref --short HEAD', [0, 'main\n', '']]])
  const port = createWithRunner(runner)

  const result = resultOf(await port.FreezeTargetBranch())
  assert.equal(result.ok, true)
  assert.equal(calls[0].cwd, '.')
})
