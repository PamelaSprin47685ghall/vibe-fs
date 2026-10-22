import test from 'node:test'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");

const change = await import('../../../dist/Change/Surface.js')
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
const ffAnswers = ({ candidate = 'cafe01', targetHead = 'beef02', branch = 'main' } = {}) => [
  ['symbolic-ref --short HEAD', [0, `${branch}\n`, '']],
  ['rev-parse HEAD', [0, `${candidate}\n`, '']],
  ['rev-parse refs/heads/main', [0, `${targetHead}\n`, '']],
  ['merge-base --is-ancestor', [0, '', '']],
  ['status --porcelain', [0, '', '']],
  ['merge --ff-only', [0, '', '']],
]
const ff = (answers) => change.gitFfMerge(git(fakeRunner(answers).runner), WORKTREE, 'main', 'beef02', 'cafe01')

test('WHAT[change-integration-002] unreadable target cleanliness cannot authorize ff publication', async () => {
  const answers = ffAnswers().map(([prefix, response]) => prefix === 'status --porcelain'
    ? [prefix, [128, '', 'cannot inspect worktree']]
    : [prefix, response])
  const fake = fakeRunner(answers)
  const result = await change.gitFfMerge(git(fake.runner), WORKTREE, 'main', 'beef02', 'cafe01')
  assert.equal(result.ok, false)
  assert.equal(fake.calls.some(call => call.args[0] === 'merge'), false)
})
test('WHAT[change-integration-002] ff publish refuses a modified target worktree and names the blocking path', async () => {
  const answers = ffAnswers().map(([prefix, response]) => prefix === 'status --porcelain'
    ? [prefix, [0, ' M dirty.fs\n', '']]
    : [prefix, response])
  const result = await ff(answers)
  assert.equal(result.ok, false)
  assert.match(result.error, /uncommitted changes; refusing ff-only merge: dirty\.fs/)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");

const change = await import('../../../dist/Change/Surface.js')

test('WHAT[change-integration-002] Git adapter reports worktree enumeration failure', async () => {
  const runner = (command) => command.args[0] === 'worktree' ? Promise.resolve([128, '', 'no .git']) : Promise.resolve([0, '', ''])
  const result = await change.gitListWorktrees(change.createGit('/repo', runner))
  assert.equal(result.ok, false)
  assert.match(result.error, /no \.git/)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");

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

test('WHAT[change-integration-002] isolated worktree creation names its source ref', async () => {
  const fake = fakeRunner([['worktree add', [0, '', '']]])
  const result = await change.gitCreateWorktree(change.createGit('/repo', fake.runner), 'job-9', PATH, 'refs/heads/main')
  assert.deepEqual(result, { ok: true, value: 'manager/job-9' })
  assert.deepEqual(fake.calls, [{
    args: ['worktree', 'add', PATH, '-b', 'manager/job-9', 'refs/heads/main'],
    cwd: '/repo',
  }])
})
}

{
const { default: assert } = await import("node:assert/strict");
const { execFileSync, spawnSync } = await import("node:child_process");
const { existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const change = await import('../../../dist/Change/Surface.js');
const hostSurface = await import('../../../dist/Change/Host/Surface.js');
const journal = await import('../../../dist/Persistence/Journal/Surface.js');
const { fakeGitPort, fakeSessions } = await import('../../verification-system/tests/support/orchestrator-host-harness.mjs');

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
    root,
    candidate,
    candidateHead,
    expectedHead,
    gitAdapter: change.createGit(repo, realRunner),
    repo,
    remove: () => rmSync(root, { recursive: true, force: true }),
  }
}

const withAdmissionFixture = async (body) => {
  const fx = fixture()
  let handle
  let host
  try {
    const opened = await journal.JournalSurface_bootWithWriterId(
      join(fx.repo, '.Herdchestra'), 'writer-worktree-boundary', 'runtime-worktree-boundary', 4242, '2026-01-01T00:00:00Z',
    )
    assert.equal(opened.ok, true, opened.error)
    handle = opened.journal
    const commands = []
    const adapter = change.createGit(fx.repo, async (command) => {
      commands.push(command)
      // Admission no longer reads `status`; only the worktree creation is the
      // controlled stop.
      assert.equal(command.args[0], 'worktree')
      assert.equal(command.args[1], 'add')
      return [1, '', 'controlled stop before worktree creation']
    })
    const sessions = fakeSessions()
    host = hostSurface.create({
      sessions,
      journal: handle,
      gitPort: {
        ...fakeGitPort(),
        CreateWorktree: (jobId, path, sourceRef) => change.gitCreateWorktree(adapter, jobId, path, sourceRef),
      },
      repoPath: fx.repo,
      targetBranch: 'main',
      orchestratorId: 'orchestrator-worktree-boundary',
    })
    const writerFile = join(fx.repo, '.Herdchestra', 'wanxiangshu', 'events', 'writer-worktree-boundary-0000.ndjson')
    const readHistory = () => {
      try {
        return readFileSync(writerFile, 'utf8')
      } catch (error) {
        if (error.code === 'ENOENT' && error.path === writerFile) return null
        throw error
      }
    }
    const history = readHistory()
    assert.equal(history, null, 'opening an empty journal must leave its lazy writer absent')
    await body({
      ...fx, host, sessions, commands, history,
      readHistory,
      fork: () => hostSurface.forkManagerJob(host, 'worktree-boundary-job', 'manager', 'Investigate the request.'),
    })
  } finally {
    try {
      if (host) await hostSurface.detachAndDrain(host)
    } finally {
      if (handle) journal.JournalSurface_dispose(handle)
      fx.remove()
    }
  }
}

integrationTest('WHAT[change-integration-002] unrelated dirty main worktree does not block a new Host job from creating its isolated worktree', async () => {
  await withAdmissionFixture(async (fx) => {
    writeFileSync(join(fx.repo, 'shared.txt'), 'dirty main worktree\n')
    const result = await fx.fork()
    assert.equal(result.ok, false)
    assert.match(result.error, /Failed to create worktree: controlled stop before worktree creation/)
    // No `status --porcelain` gate ran at admission: the first Git verb is the
    // worktree creation, named from the frozen source ref.
    assert.equal(fx.commands[0].args[0], 'worktree')
    assert.deepEqual(fx.commands[0].args.slice(1), ['add', join(fx.repo, '.worktrees', 'worktree-boundary-job'), '-b', 'manager/worktree-boundary-job', 'main'])
    assert.deepEqual(fx.sessions.calls, [])
    assert.equal(typeof fx.readHistory(), 'string', 'admission must append the worktree request')
    assert.match(fx.readHistory(), /WorktreeCreateRequested/)
    assert.equal(git(fx.repo, 'rev-parse', 'HEAD'), fx.expectedHead)
  })
})

integrationTest('WHAT[change-integration-002] isolated worktree creation sees the committed ref, not the main worktree drafts', async () => {
  const fx = fixture()
  try {
    writeFileSync(join(fx.repo, 'draft.txt'), 'uncommitted draft\n')
    const worktree = join(fx.root, 'isolated')
    const created = await change.gitCreateWorktree(fx.gitAdapter, 'job-1', worktree, 'refs/heads/main')
    assert.equal(created.ok, true, created.error)
    assert.equal(created.value, 'manager/job-1')

    // The main worktree's draft is neither copied nor dropped.
    assert.equal(readFileSync(join(fx.repo, 'draft.txt'), 'utf8'), 'uncommitted draft\n')
    assert.equal(existsSync(join(worktree, 'draft.txt')), false, 'isolated worktree must not copy the draft')

    // The isolated worktree is pinned to the committed target head.
    assert.equal(git(worktree, 'rev-parse', 'HEAD'), fx.expectedHead)
    assert.equal(git(worktree, 'rev-parse', '--abbrev-ref', 'HEAD'), 'manager/job-1')
  } finally {
    fx.remove()
  }
})

integrationTest('WHAT[change-integration-002] Adapter_ffMerge_dirty_target_fails_closed_without_advancing_head', async () => {
  const fx = fixture()

  try {
    writeFileSync(join(fx.repo, 'shared.txt'), 'dirty\n')
    const result = await change.gitFfMerge(fx.gitAdapter, fx.candidate, 'main', fx.expectedHead, fx.candidateHead)

    assert.equal(result.ok, false)
    assert.match(result.error, /uncommitted changes; refusing ff-only merge: shared\.txt/)
    assert.equal(git(fx.repo, 'rev-parse', 'refs/heads/main'), fx.expectedHead)
    assert.equal(git(fx.candidate, 'rev-parse', 'HEAD'), fx.candidateHead)
  } finally {
    fx.remove()
  }
})
}
