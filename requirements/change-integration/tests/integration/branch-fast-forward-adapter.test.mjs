// CHGINT-002/008 — the registered Change Surface ffMerge adapter executes real Git.

import assert from 'node:assert/strict'
import { execFileSync, spawnSync } from 'node:child_process'
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

const change = await import('../../../../dist/Change/Surface.js')

const git = (cwd, ...args) => execFileSync('git', args, { cwd, encoding: 'utf8' }).trim()

const commit = (repo, file, contents, message) => {
  writeFileSync(join(repo, file), contents)
  git(repo, 'add', file)
  git(repo, 'commit', '--quiet', '-m', message)
  return git(repo, 'rev-parse', 'HEAD')
}

const realRunner = (command) => {
  const result = spawnSync(command.fileName, command.args, {
    cwd: command.workingDirectory,
    encoding: 'utf8',
  })
  return Promise.resolve([result.status ?? 1, result.stdout ?? '', result.stderr ?? ''])
}

const fixture = () => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-ff-merge-real-'))
  const repo = join(root, 'repo')
  const candidate = join(root, 'candidate')

  execFileSync('git', ['init', '--quiet', repo])
  git(repo, 'config', 'user.email', 'test@example.com')
  git(repo, 'config', 'user.name', 'test')
  git(repo, 'checkout', '--quiet', '-b', 'main')
  const expectedHead = commit(repo, 'shared.txt', 'base\n', 'base')
  git(repo, 'worktree', 'add', '--quiet', '-b', 'candidate', candidate, 'main')
  const candidateHead = commit(candidate, 'candidate.txt', 'candidate\n', 'candidate')

  return {
    candidate,
    candidateHead,
    expectedHead,
    gitAdapter: change.createGit(repo, realRunner),
    repo,
    remove: () => rmSync(root, { recursive: true, force: true }),
  }
}

test('WHAT[CHGINT-008] Adapter_ffMerge_clean_fast_forward_returns_exact_candidate_receipt', async () => {
  const fx = fixture()

  try {
    const result = await change.gitFfMerge(fx.gitAdapter, fx.candidate, 'main', fx.expectedHead)

    assert.deepEqual(result, { ok: true, value: fx.candidateHead })
    assert.equal(git(fx.repo, 'symbolic-ref', '--short', 'HEAD'), 'main')
    assert.equal(git(fx.repo, 'rev-parse', 'refs/heads/main'), fx.candidateHead)
  } finally {
    fx.remove()
  }
})

test('WHAT[CHGINT-002] Adapter_ffMerge_dirty_target_fails_closed_without_advancing_head', async () => {
  const fx = fixture()

  try {
    writeFileSync(join(fx.repo, 'shared.txt'), 'dirty\n')
    const result = await change.gitFfMerge(fx.gitAdapter, fx.candidate, 'main', fx.expectedHead)

    assert.deepEqual(result, { ok: false, error: 'target worktree is dirty; refusing ff-only merge' })
    assert.equal(git(fx.repo, 'rev-parse', 'refs/heads/main'), fx.expectedHead)
    assert.equal(git(fx.candidate, 'rev-parse', 'HEAD'), fx.candidateHead)
  } finally {
    fx.remove()
  }
})

test('WHAT[CHGINT-008] Adapter_ffMerge_moved_head_fails_closed_without_advancing_head', async () => {
  const fx = fixture()

  try {
    const movedHead = commit(fx.repo, 'moved.txt', 'moved\n', 'move target')
    const result = await change.gitFfMerge(fx.gitAdapter, fx.candidate, 'main', fx.expectedHead)

    assert.deepEqual(result, { ok: false, error: 'target ref moved' })
    assert.equal(git(fx.repo, 'rev-parse', 'refs/heads/main'), movedHead)
    assert.equal(git(fx.candidate, 'rev-parse', 'HEAD'), fx.candidateHead)
  } finally {
    fx.remove()
  }
})
