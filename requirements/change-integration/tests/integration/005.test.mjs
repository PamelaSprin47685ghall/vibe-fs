// CHGINT-005 — the owner surface emits argv accepted by real Git.

import assert from 'node:assert/strict'
import { execFileSync, spawnSync } from 'node:child_process'
import { chmodSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

const change = await import('../../../../dist/Change/Surface.js')

const seedRepo = (repo) => {
  execFileSync('git', ['init', '--quiet', repo])
  execFileSync('git', ['config', 'user.email', 'test@example.com'], { cwd: repo })
  execFileSync('git', ['config', 'user.name', 'test'], { cwd: repo })
  writeFileSync(join(repo, 'seed.txt'), 'seed\n')
  execFileSync('git', ['add', 'seed.txt'], { cwd: repo })
  execFileSync('git', ['commit', '--quiet', '-m', 'seed'], { cwd: repo })
}

const realRunner = (command) => {
  const result = spawnSync(command.fileName, command.args, {
    cwd: command.workingDirectory,
    encoding: 'utf8',
  })
  return Promise.resolve([result.status ?? 1, result.stdout ?? '', result.stderr ?? ''])
}

const createRealWorktree = async (repo, child, job) => {
  const git = change.createGit(repo, realRunner)
  return change.gitCreateWorktree(git, job, child)
}

test('WHAT[CHGINT-005] CHGINT_worktree_create_argv_is_accepted_by_real_git', async () => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-worktree-real-'))
  const repo = join(root, 'repo')
  const child = join(root, 'manager-job-real')

  try {
    seedRepo(repo)
    const created = await createRealWorktree(repo, child, 'job-real')
    assert.equal(created.ok, true, created.ok ? '' : created.error)
    assert.equal(created.value, 'manager/job-real')
    assert.equal(execFileSync('git', ['-C', child, 'branch', '--show-current'], { encoding: 'utf8' }).trim(), 'manager/job-real')
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})

test('WHAT[CHGINT-005] CHGINT_worktree_create_survives_installed_wanxiang_reference_transaction_hook', async () => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-worktree-hooked-'))
  const repo = join(root, 'repo')
  const child = join(root, 'manager-job-hooked')

  try {
    seedRepo(repo)
    const hook = join(repo, '.git', 'hooks', 'reference-transaction')
    writeFileSync(hook, '#!/bin/sh\nexit 0\n')
    chmodSync(hook, 0o755)

    const created = await createRealWorktree(repo, child, 'job-hooked')
    assert.equal(created.ok, true, created.ok ? '' : created.error)
    assert.equal(execFileSync('git', ['-C', child, 'branch', '--show-current'], { encoding: 'utf8' }).trim(), 'manager/job-hooked')
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})
