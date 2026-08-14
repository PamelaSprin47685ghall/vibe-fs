// Real Git parser proof for CHGINT / PERSIST-009 worktree creation.
// Fake runner tests pin typed wiring; these tests prove the emitted argv is accepted by Git,
// including after Wanxiangshu installs its reference-transaction/pre-push hooks.

import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import { managerJobId, resultOf, worktreePath } from '../../../verification-system/tests/support/domain.mjs'

const { WorktreeCommands_create } = await import('../../../../dist/Git/WorktreeResource.js')
const { run: runGitCommand } = await import('../../../../dist/Change/Host/GitAdapter.js')
const { ensure: ensureWanxiangHooks } = await import('../../../../dist/Git/Hook/Dispatcher.js')

const seedRepo = (repo) => {
  execFileSync('git', ['init', '--quiet', repo])
  execFileSync('git', ['config', 'user.email', 'test@example.com'], { cwd: repo })
  execFileSync('git', ['config', 'user.name', 'test'], { cwd: repo })
  writeFileSync(join(repo, 'seed.txt'), 'seed\n')
  execFileSync('git', ['add', 'seed.txt'], { cwd: repo })
  execFileSync('git', ['commit', '--quiet', '-m', 'seed'], { cwd: repo })
}

const createRealWorktree = async (repo, child, job) =>
  resultOf(await WorktreeCommands_create(runGitCommand, repo, managerJobId(job), worktreePath(child)))

test('CHGINT_worktree_create_argv_is_accepted_by_real_git', async () => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-worktree-real-'))
  const repo = join(root, 'repo')
  const child = join(root, 'manager-job-real')

  try {
    seedRepo(repo)
    const created = await createRealWorktree(repo, child, 'job-real')

    assert.equal(created.ok, true, created.ok ? '' : created.error)
    assert.equal(created.value.fields[0], 'manager/job-real')
    assert.equal(execFileSync('git', ['-C', child, 'branch', '--show-current'], { encoding: 'utf8' }).trim(), 'manager/job-real')
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})

test('CHGINT_worktree_create_survives_installed_wanxiang_reference_transaction_hook', async () => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-worktree-hooked-'))
  const repo = join(root, 'repo')
  const child = join(root, 'manager-job-hooked')

  try {
    seedRepo(repo)
    const ensured = resultOf(ensureWanxiangHooks(repo))
    assert.equal(ensured.ok, true, ensured.ok ? '' : ensured.error)

    const created = await createRealWorktree(repo, child, 'job-hooked')
    assert.equal(created.ok, true, created.ok ? '' : created.error)
    assert.equal(execFileSync('git', ['-C', child, 'branch', '--show-current'], { encoding: 'utf8' }).trim(), 'manager/job-hooked')
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})
