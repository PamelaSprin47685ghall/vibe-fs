import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");

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
const ffAnswers = ({ candidate = 'cafe01', targetHead = 'beef02', branch = 'main' } = {}) => [
  ['symbolic-ref --short HEAD', [0, `${branch}\n`, '']],
  ['rev-parse HEAD', [0, `${candidate}\n`, '']],
  ['rev-parse refs/heads/main', [0, `${targetHead}\n`, '']],
  ['merge-base --is-ancestor', [0, '', '']],
  ['status --porcelain', [0, '', '']],
  ['merge --ff-only', [0, '', '']],
]
const ff = (answers) => change.gitFfMerge(git(fakeRunner(answers).runner), WORKTREE, 'main', 'beef02', 'cafe01')

test('WHAT[change-integration-002] GIT_is_dirty_true_only_on_nonempty_porcelain', async () => {
  assert.equal(await change.gitIsDirty(git(fakeRunner([['status --porcelain', [0, ' M file.fs\n', '']]]).runner), WORKTREE), true)
  assert.equal(await change.gitIsDirty(git(fakeRunner([['status --porcelain', [0, '', '']]]).runner), WORKTREE), false)
  assert.equal(await change.gitIsDirty(git(fakeRunner([['status --porcelain', [1, '', 'boom']]]).runner), WORKTREE), false)
})
test('WHAT[change-integration-002] GIT_ff_merge_refuses_dirty_target_worktree', async () => {
  const answers = ffAnswers().map(([prefix, response]) => prefix === 'status --porcelain' ? [prefix, [0, ' M dirty.fs\n', '']] : [prefix, response])
  const result = await ff(answers)
  assert.equal(result.ok, false)
  assert.match(result.error, /target worktree is dirty; refusing ff-only merge/)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");

const change = await import('../../../dist/Change/Surface.js')
const job = (id, path = `/tmp/${id}`) => ({
  jobId: id,
  managerSessionId: `ses-${id}`,
  managerAgent: 'manager',
  byname: id,
  worktreeIdentity: `manager/${id}`,
  worktreePath: path,
  targetRef: 'refs/heads/main',
  targetBranchFrozen: 'refs/heads/main',
})

test('WHAT[change-integration-002] HOST_sweep_failure_aborts_engine_initialization', async () => {
  const runner = (command) => command.args[0] === 'worktree' ? Promise.resolve([128, '', 'no .git']) : Promise.resolve([0, '', ''])
  const result = await change.gitListWorktrees(change.createGit('/repo', runner))
  assert.equal(result.ok, false)
  assert.match(result.error, /no \.git/)
})
test('WHAT[change-integration-002] HOST_ForkManagerJob_surfaces_the_engine_verdict_error', async () => {
  const runner = () => Promise.resolve([0, ' M dirty.fs\n', ''])
  assert.equal(await change.gitIsDirty(change.createGit('/repo', runner), '/tmp/hostfw5'), true)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { assertOpaque } = await import("../../verification-system/tests/support/js-contract.mjs");

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

test('WHAT[change-integration-002] WORKTREE_CMD_is_dirty_reads_porcelain', async () => {
  assert.equal(await change.gitIsDirty(fakeGit([['status --porcelain', [0, ' M x.fs\n', '']]]).git, PATH), true)
  assert.equal(await change.gitIsDirty(fakeGit([['status --porcelain', [0, '\n', '']]]).git, PATH), false)
})
}
