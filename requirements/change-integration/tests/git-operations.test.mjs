// CHGINT-002/003/005/008 — typed Git verbs through the owner surface.

import assert from 'node:assert/strict'
import test from 'node:test'

const change = await import('../../../dist/Change/Surface.js')
const hostSurface = await import('../../../dist/Change/Host/Surface.js')

const fakeRunner = (answers) => {
  const calls = []
  const runner = (command) => {
    const args = command.args
    calls.push({ file: command.fileName, args, cwd: command.workingDirectory })
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
const git = (runner) => change.createGit(REPO, runner)
const ok = (result) => {
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.value
}

// ── FreezeTargetBranch ───────────────────────────────────────────────────────

test('WHAT[CHGINT-008] GIT_freeze_target_branch_reads_symbolic_ref', async () => {
  const port = git(fakeRunner([['symbolic-ref --short HEAD', [0, 'main\n', '']]]).runner)
  assert.equal((await change.gitFreezeTargetBranch(port)).value, 'main')
})

test('WHAT[CHGINT-008] GIT_freeze_target_branch_refuses_detached_head', async () => {
  const port = git(fakeRunner([['symbolic-ref --short HEAD', [128, '', 'fatal: ref HEAD is not a symbolic ref']]]).runner)
  const result = await change.gitFreezeTargetBranch(port)
  assert.equal(result.ok, false)
  assert.match(result.error, /fatal: ref HEAD is not a symbolic ref/)
})

test('WHAT[CHGINT-008] GIT_freeze_target_branch_blank_stdout_is_detached', async () => {
  const port = git(fakeRunner([['symbolic-ref --short HEAD', [0, '  \n', '']]]).runner)
  const result = await change.gitFreezeTargetBranch(port)
  assert.equal(result.ok, false)
  assert.match(result.error, /detached/)
})

// ── IsDirty ──────────────────────────────────────────────────────────────────

test('WHAT[CHGINT-002] GIT_is_dirty_true_only_on_nonempty_porcelain', async () => {
  assert.equal(await change.gitIsDirty(git(fakeRunner([['status --porcelain', [0, ' M file.fs\n', '']]]).runner), WORKTREE), true)
  assert.equal(await change.gitIsDirty(git(fakeRunner([['status --porcelain', [0, '', '']]]).runner), WORKTREE), false)
  assert.equal(await change.gitIsDirty(git(fakeRunner([['status --porcelain', [1, '', 'boom']]]).runner), WORKTREE), false)
})

// ── Rebase / ConflictedFiles / HasRebaseHead ─────────────────────────────────

test('WHAT[CHGINT-003] GIT_rebase_ok_on_zero_exit', async () => {
  const result = await change.gitRebase(git(fakeRunner([['rebase main', [0, '', '']]]).runner), WORKTREE, 'main')
  assert.equal(result.ok, true)
})

test('WHAT[CHGINT-003] GIT_rebase_stale_rebase_head_is_cleared_before_fresh_rebase', async () => {
  const fake = fakeRunner([])
  await change.gitRebase(git(fake.runner), WORKTREE, 'main')
  assert.deepEqual(fake.calls.map((call) => call.args.join(' ')), [
    'rev-parse --git-path rebase-merge',
    'rev-parse --git-path rebase-apply',
    'update-ref -d REBASE_HEAD',
    'rebase main',
  ])
})

test('WHAT[CHGINT-003] GIT_rebase_in_progress_stages_and_continues', async () => {
  const { mkdtempSync, rmSync } = await import('node:fs')
  const { tmpdir } = await import('node:os')
  const { join } = await import('node:path')
  const dir = mkdtempSync(join(tmpdir(), 'wxs-rebase-'))
  const fake = fakeRunner([['rev-parse --git-path rebase-merge', [0, `${dir}\n`, '']]])
  const result = await change.gitRebase(git(fake.runner), WORKTREE, 'main')
  assert.equal(result.ok, true)
  assert.deepEqual(fake.calls.map((call) => call.args.join(' ')), [
    'rev-parse --git-path rebase-merge',
    'rev-parse --git-path rebase-apply',
    'add -A',
    '-c core.editor=true rebase --continue',
  ])
  rmSync(dir, { recursive: true, force: true })
})

test('WHAT[CHGINT-003] GIT_rebase_continue_failure_surfaces_stderr', async () => {
  const { mkdtempSync, rmSync } = await import('node:fs')
  const { tmpdir } = await import('node:os')
  const { join } = await import('node:path')
  const dir = mkdtempSync(join(tmpdir(), 'wxs-rebase-fail-'))
  const fake = fakeRunner([
    ['rev-parse --git-path rebase-apply', [0, `${dir}\n`, '']],
    ['-c core.editor=true rebase --continue', [1, '', 'conflict remains']],
  ])
  const result = await change.gitRebase(git(fake.runner), WORKTREE, 'main')
  assert.equal(result.ok, false)
  assert.equal(result.error, 'conflict remains')
  rmSync(dir, { recursive: true, force: true })
})

test('WHAT[CHGINT-003] GIT_rebase_stage_failure_is_an_error', async () => {
  const { mkdtempSync, rmSync } = await import('node:fs')
  const { tmpdir } = await import('node:os')
  const { join } = await import('node:path')
  const dir = mkdtempSync(join(tmpdir(), 'wxs-rebase-stage-'))
  const fake = fakeRunner([
    ['rev-parse --git-path rebase-merge', [0, `${dir}\n`, '']],
    ['add -A', [1, '', 'index locked']],
  ])
  const result = await change.gitRebase(git(fake.runner), WORKTREE, 'main')
  assert.equal(result.ok, false)
  assert.equal(result.error, 'index locked')
  rmSync(dir, { recursive: true, force: true })
})

test('WHAT[CHGINT-003] GIT_rebase_surfaces_stderr_on_failure', async () => {
  const result = await change.gitRebase(git(fakeRunner([['rebase main', [1, 'stdout-noise', 'CONFLICT (content)']]]).runner), WORKTREE, 'main')
  assert.equal(result.ok, false)
  assert.equal(result.error, 'CONFLICT (content)')
})

test('WHAT[CHGINT-003] GIT_candidate_commit_deletes_stale_rebase_head_before_commit_and_surfaces_failure', async () => {
  const fake = fakeRunner([['commit -m candidate: manager-1', [1, '', 'commit rejected']]])
  const result = await hostSurface.finalizeWorktree(fake.runner, 'manager-1', WORKTREE)
  assert.equal(result.ok, false)
  assert.equal(result.error, 'git commit failed: commit rejected')
  assert.deepEqual(fake.calls.slice(-2).map((call) => call.args.join(' ')), [
    'update-ref -d REBASE_HEAD',
    'commit -m candidate: manager-1',
  ])
})

test('WHAT[CHGINT-005] GIT_conflicted_files_parses_lines', async () => {
  const result = await change.gitConflictedFiles(git(fakeRunner([['diff --name-only --diff-filter=U', [0, 'a.fs\nb.fs\n', '']]]).runner), WORKTREE)
  assert.deepEqual(ok(result), ['a.fs', 'b.fs'])
})

test('WHAT[CHGINT-005] GIT_conflicted_files_error_propagates', async () => {
  const result = await change.gitConflictedFiles(git(fakeRunner([['diff --name-only --diff-filter=U', [1, '', 'not a repo']]]).runner), WORKTREE)
  assert.equal(result.ok, false)
  assert.equal(result.error, 'not a repo')
})

test('WHAT[CHGINT-003] GIT_has_rebase_head_true_only_when_git_path_dir_exists', async () => {
  const { mkdtempSync, rmSync } = await import('node:fs')
  const { tmpdir } = await import('node:os')
  const { join } = await import('node:path')
  const dir = mkdtempSync(join(tmpdir(), 'wxs-rebase-head-'))
  const fake = fakeRunner([['rev-parse --git-path rebase-merge', [0, `${dir}\n`, '']]])
  assert.equal(await change.gitHasRebaseHead(git(fake.runner), WORKTREE), true)
  assert.deepEqual(fake.calls.map((call) => call.args.join(' ')), [
    'rev-parse --git-path rebase-merge',
    'rev-parse --git-path rebase-apply',
  ])
  assert.equal(await change.gitHasRebaseHead(git(fakeRunner([['rev-parse --git-path rebase-merge', [0, `${dir}-gone\n`, '']]]).runner), WORKTREE), false)
  assert.equal(await change.gitHasRebaseHead(git(fakeRunner([]).runner), WORKTREE), false)
  rmSync(dir, { recursive: true, force: true })
})

// ── ReadHead / GetTargetHead ─────────────────────────────────────────────────

test('WHAT[CHGINT-008] GIT_read_head_returns_commit_hash', async () => {
  const result = await change.gitReadHead(git(fakeRunner([['rev-parse HEAD', [0, 'deadbeef\n', '']]]).runner), WORKTREE)
  assert.equal(ok(result), 'deadbeef')
})

test('WHAT[CHGINT-008] GIT_read_head_empty_stdout_is_missing', async () => {
  const result = await change.gitReadHead(git(fakeRunner([['rev-parse HEAD', [0, '  \n', '']]]).runner), WORKTREE)
  assert.equal(result.ok, false)
  assert.equal(result.error, 'HEAD is empty')
})

test('WHAT[CHGINT-008] GIT_get_target_head_missing_branch', async () => {
  const result = await change.gitGetTargetHead(git(fakeRunner([['rev-parse refs/heads/main', [128, '', '']]]).runner), 'main')
  assert.equal(result.ok, false)
  assert.match(result.error, /target branch not found: main/)
})

const ffAnswers = ({ candidate = 'cafe01', targetHead = 'beef02', branch = 'main' } = {}) => [
  ['symbolic-ref --short HEAD', [0, `${branch}\n`, '']],
  ['rev-parse HEAD', [0, `${candidate}\n`, '']],
  ['rev-parse refs/heads/main', [0, `${targetHead}\n`, '']],
  ['merge-base --is-ancestor', [0, '', '']],
  ['status --porcelain', [0, '', '']],
  ['merge --ff-only', [0, '', '']],
]

const ff = (answers) => change.gitFfMerge(git(fakeRunner(answers).runner), WORKTREE, 'main', 'beef02')

test('WHAT[CHGINT-008] GIT_ff_merge_happy_path_advances_to_candidate', async () => {
  const fake = fakeRunner(ffAnswers())
  const result = await change.gitFfMerge(git(fake.runner), WORKTREE, 'main', 'beef02')
  assert.equal(ok(result), 'cafe01')
  assert.deepEqual(fake.calls.map((call) => call.args[0]), ['rev-parse', 'symbolic-ref', 'rev-parse', 'merge-base', 'status', 'merge', 'rev-parse'])
})

test('WHAT[CHGINT-008] GIT_ff_merge_refuses_when_repo_on_wrong_branch', async () => {
  const result = await ff(ffAnswers({ branch: 'feature' }))
  assert.equal(result.ok, false)
  assert.match(result.error, /publish branch mismatch: target repo is on 'feature' but publish is frozen to 'main'/)
})

test('WHAT[CHGINT-008] GIT_ff_merge_refuses_detached_with_placeholder', async () => {
  const result = await ff([
    ['rev-parse HEAD', [0, 'cafe01\n', '']],
    ['symbolic-ref --short HEAD', [128, '', '']],
  ])
  assert.equal(result.ok, false)
  assert.match(result.error, /<detached HEAD>/)
})

test('WHAT[CHGINT-008] GIT_ff_merge_refuses_when_target_moved_since_head_read', async () => {
  const result = await ff(ffAnswers({ targetHead: 'other9' }))
  assert.equal(result.ok, false)
  assert.equal(result.error, 'target ref moved')
})

test('WHAT[CHGINT-008] GIT_ff_merge_refuses_non_fast_forward_candidate', async () => {
  const answers = ffAnswers().map(([prefix, response]) => prefix === 'merge-base --is-ancestor' ? [prefix, [1, '', '']] : [prefix, response])
  const result = await ff(answers)
  assert.equal(result.ok, false)
  assert.equal(result.error, 'candidate is not a fast-forward of the target branch')
})

test('WHAT[CHGINT-002] GIT_ff_merge_refuses_dirty_target_worktree', async () => {
  const answers = ffAnswers().map(([prefix, response]) => prefix === 'status --porcelain' ? [prefix, [0, ' M dirty.fs\n', '']] : [prefix, response])
  const result = await ff(answers)
  assert.equal(result.ok, false)
  assert.match(result.error, /target worktree is dirty; refusing ff-only merge/)
})

test('WHAT[CHGINT-008] GIT_ff_merge_ref_moved_lock_diagnostic_maps_to_cas_error', async () => {
  const answers = ffAnswers().map(([prefix, response]) => prefix === 'merge --ff-only' ? [prefix, [1, '', 'error: cannot lock ref refs/heads/main: is at x but expected y']] : [prefix, response])
  const result = await ff(answers)
  assert.equal(result.ok, false)
  assert.equal(result.error, 'target ref moved')
})

test('WHAT[CHGINT-008] GIT_ff_merge_generic_merge_failure_surfaces_message', async () => {
  const answers = ffAnswers().map(([prefix, response]) => prefix === 'merge --ff-only' ? [prefix, [1, '', 'merge exploded']] : [prefix, response])
  const result = await ff(answers)
  assert.equal(result.ok, false)
  assert.equal(result.error, 'merge exploded')
})

test('WHAT[CHGINT-008] GIT_ff_merge_empty_candidate_head_is_an_error', async () => {
  const result = await ff([['rev-parse HEAD', [0, ' \n', '']]])
  assert.equal(result.ok, false)
  assert.equal(result.error, 'candidate HEAD is empty')
})

test('WHAT[CHGINT-008] GIT_ff_merge_verify_head_mismatch_reports_actual', async () => {
  const fake = fakeRunner([
    ['symbolic-ref --short HEAD', [0, 'main\n', '']],
    ['merge-base --is-ancestor', [0, '', '']],
    ['status --porcelain', [0, '', '']],
    ['merge --ff-only', [0, '', '']],
    ['rev-parse refs/heads/main', [0, 'beef02\n', '']],
    ['rev-parse HEAD', [[0, 'cafe01\n', ''], [0, 'wrong00\n', '']]],
  ])
  const result = await change.gitFfMerge(git(fake.runner), WORKTREE, 'main', 'beef02')
  assert.equal(result.ok, false)
  assert.match(result.error, /ff-only merge did not advance HEAD to candidate cafe01 \(got wrong00\)/)
})

test('WHAT[CHGINT-008] GIT_create_with_runner_binds_dot_repo', async () => {
  const fake = fakeRunner([['symbolic-ref --short HEAD', [0, 'main\n', '']]])
  const port = change.createGit('.', fake.runner)
  assert.equal((await change.gitFreezeTargetBranch(port)).ok, true)
  assert.equal(fake.calls[0].cwd, '.')
})
