import test from 'node:test'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'

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

test('WHAT[change-integration-008] GIT_freeze_target_branch_reads_symbolic_ref', async () => {
  const port = git(fakeRunner([['symbolic-ref --short HEAD', [0, 'main\n', '']]]).runner)
  assert.equal((await change.gitFreezeTargetBranch(port)).value, 'main')
})
test('WHAT[change-integration-008] GIT_freeze_target_branch_refuses_detached_head', async () => {
  const port = git(fakeRunner([['symbolic-ref --short HEAD', [128, '', 'fatal: ref HEAD is not a symbolic ref']]]).runner)
  const result = await change.gitFreezeTargetBranch(port)
  assert.equal(result.ok, false)
  assert.match(result.error, /fatal: ref HEAD is not a symbolic ref/)
})
test('WHAT[change-integration-008] GIT_freeze_target_branch_blank_stdout_is_detached', async () => {
  const port = git(fakeRunner([['symbolic-ref --short HEAD', [0, '  \n', '']]]).runner)
  const result = await change.gitFreezeTargetBranch(port)
  assert.equal(result.ok, false)
  assert.match(result.error, /detached/)
})
test('WHAT[change-integration-008] GIT_read_head_returns_commit_hash', async () => {
  const result = await change.gitReadHead(git(fakeRunner([['rev-parse HEAD', [0, 'deadbeef\n', '']]]).runner), WORKTREE)
  assert.equal(ok(result), 'deadbeef')
})
test('WHAT[change-integration-008] GIT_read_head_empty_stdout_is_missing', async () => {
  const result = await change.gitReadHead(git(fakeRunner([['rev-parse HEAD', [0, '  \n', '']]]).runner), WORKTREE)
  assert.equal(result.ok, false)
  assert.equal(result.error, 'HEAD is empty')
})
test('WHAT[change-integration-008] GIT_get_target_head_missing_branch', async () => {
  const result = await change.gitGetTargetHead(git(fakeRunner([['rev-parse refs/heads/main', [128, '', '']]]).runner), 'main')
  assert.equal(result.ok, false)
  assert.match(result.error, /target branch not found: main/)
})
test('WHAT[change-integration-008] GIT_ff_merge_happy_path_advances_to_candidate', async () => {
  const fake = fakeRunner(ffAnswers())
  const result = await change.gitFfMerge(git(fake.runner), WORKTREE, 'main', 'beef02', 'cafe01')
  assert.equal(ok(result), 'cafe01')
  assert.deepEqual(fake.calls.map((call) => call.args[0]), ['rev-parse', 'symbolic-ref', 'rev-parse', 'merge-base', 'status', 'merge', 'rev-parse'])
})
test('WHAT[change-integration-008] GIT_ff_merge_refuses_when_repo_on_wrong_branch', async () => {
  const result = await ff(ffAnswers({ branch: 'feature' }))
  assert.equal(result.ok, false)
  assert.match(result.error, /publish branch mismatch: target repo is on 'feature' but publish is frozen to 'main'/)
})
test('WHAT[change-integration-008] GIT_ff_merge_refuses_detached_with_placeholder', async () => {
  const result = await ff([
    ['rev-parse HEAD', [0, 'cafe01\n', '']],
    ['symbolic-ref --short HEAD', [128, '', '']],
  ])
  assert.equal(result.ok, false)
  assert.match(result.error, /<detached HEAD>/)
})
test('WHAT[change-integration-008] GIT_ff_merge_refuses_when_target_moved_since_head_read', async () => {
  const result = await ff(ffAnswers({ targetHead: 'other9' }))
  assert.equal(result.ok, false)
  assert.equal(result.error, 'target ref moved')
})
test('WHAT[change-integration-008] GIT_ff_merge_refuses_non_fast_forward_candidate', async () => {
  const answers = ffAnswers().map(([prefix, response]) => prefix === 'merge-base --is-ancestor' ? [prefix, [1, '', '']] : [prefix, response])
  const result = await ff(answers)
  assert.equal(result.ok, false)
  assert.equal(result.error, 'candidate is not a fast-forward of the target branch')
})
test('WHAT[change-integration-008] GIT_ff_merge_ref_moved_lock_diagnostic_maps_to_cas_error', async () => {
  const answers = ffAnswers().map(([prefix, response]) => prefix === 'merge --ff-only' ? [prefix, [1, '', 'error: cannot lock ref refs/heads/main: is at x but expected y']] : [prefix, response])
  const result = await ff(answers)
  assert.equal(result.ok, false)
  assert.equal(result.error, 'target ref moved')
})
test('WHAT[change-integration-008] GIT_ff_merge_generic_merge_failure_surfaces_message', async () => {
  const answers = ffAnswers().map(([prefix, response]) => prefix === 'merge --ff-only' ? [prefix, [1, '', 'merge exploded']] : [prefix, response])
  const result = await ff(answers)
  assert.equal(result.ok, false)
  assert.equal(result.error, 'merge exploded')
})
test('WHAT[change-integration-008] GIT_ff_merge_empty_candidate_head_is_an_error', async () => {
  const result = await ff([['rev-parse HEAD', [0, ' \n', '']]])
  assert.equal(result.ok, false)
  assert.equal(result.error, 'candidate HEAD is empty')
})
test('WHAT[change-integration-008] GIT_ff_merge_verify_head_mismatch_reports_actual', async () => {
  const fake = fakeRunner([
    ['symbolic-ref --short HEAD', [0, 'main\n', '']],
    ['merge-base --is-ancestor', [0, '', '']],
    ['status --porcelain', [0, '', '']],
    ['merge --ff-only', [0, '', '']],
    ['rev-parse refs/heads/main', [0, 'beef02\n', '']],
    ['rev-parse HEAD', [[0, 'cafe01\n', ''], [0, 'wrong00\n', '']]],
  ])
  const result = await change.gitFfMerge(git(fake.runner), WORKTREE, 'main', 'beef02', 'cafe01')
  assert.equal(result.ok, false)
  assert.match(result.error, /ff-only merge did not advance HEAD to candidate cafe01 \(got wrong00\)/)
})
test('WHAT[change-integration-008] GIT_create_with_runner_binds_dot_repo', async () => {
  const fake = fakeRunner([['symbolic-ref --short HEAD', [0, 'main\n', '']]])
  const port = change.createGit('.', fake.runner)
  assert.equal((await change.gitFreezeTargetBranch(port)).ok, true)
  assert.equal(fake.calls[0].cwd, '.')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");

const change = await import('../../../dist/Change/Surface.js')
const JOB_A = 'job_a'
const JOB_B = 'job_b'
const createEvent = (jobId, managerSessionId, worktreeIdentity = `wt_${jobId.slice(-1)}`) => ({
  kind: 'ManagerJobCreated',
  payload: {
    jobId,
    managerSessionId,
    managerAgent: 'manager',
    byname: 'Road',
    worktreeIdentity,
    worktreePath: `/tmp/${worktreeIdentity}`,
    targetRef: 'refs/heads/main',
    targetBranchFrozen: 'refs/heads/main',
  },
})
const candidateEvent = (
  jobId,
  candidateCommit = 'c1',
  workspaceSnapshotId = 'snapshot-1',
  qualityCertificateId = 'certificate-1',
) => ({
  kind: 'CandidateReady',
  payload: { jobId, candidateCommit, workspaceSnapshotId, qualityCertificateId },
})
const conflictEvent = (jobId, { candidateCommit = 'c1', targetHeadSnapshot = 'h1', conflictFiles = ['publish_proof.txt'] } = {}) => ({
  kind: 'ConflictDetected',
  payload: {
    jobId,
    candidateCommit,
    targetHeadSnapshot,
    workspaceSnapshotId: 'snapshot-conflict',
    conflictFiles,
    diagnosticsDigest: 'conflict-digest',
  },
})
const rebasedEvent = (
  jobId,
  { rebasedCommit = 'r1', targetHeadSnapshot = 'h1', workspaceSnapshotId = 'snapshot-rebased' } = {},
) => ({
  kind: 'RebasedCandidateReady',
  payload: { jobId, rebasedCommit, targetHeadSnapshot, workspaceSnapshotId },
})
const publishClaimedEvent = (jobId, expectedHead = 'h1') => ({
  kind: 'PublishClaimed',
  payload: {
    jobId,
    targetRef: 'refs/heads/main',
    rebasedCommit: 'r1',
    expectedHead,
    workspaceSnapshotId: 'snapshot-rebased',
    qualityCertificateId: 'certificate-1',
    authorityRevision: 'authority-1',
  },
})
const publishedEvent = (jobId, candidateCommit = 'c1', resultingTargetHead = 'r1') => ({
  kind: 'Published',
  payload: { jobId, candidateCommit, resultingTargetHead },
})
const foldEvents = (events) => {
  const result = change.fold(events)
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.value
}
const factsOf = (projection, jobId) => change.find(projection, jobId).facts
const classifyRebased = (head, rebasedCommit = 'r1', snapshot = 'h1') =>
  change.classifyRebasedCandidate(head ?? null, rebasedCommit, snapshot)
const classifyClaim = (head, rebasedCommit = 'r1', expectedHead = 'h1') =>
  change.classifyPublishClaim(head ?? null, rebasedCommit, expectedHead)

test('WHAT[change-integration-008] THEOREM_unreadable_target_head_fails_closed', () => {
  assert.equal(classifyRebased(undefined).kind, 'HeadUnreadable')
  assert.equal(classifyClaim(undefined).kind, 'HeadUnreadable')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");

const change = await import('../../../dist/Change/Surface.js')

test('WHAT[change-integration-008] reentry with candidate moving between read and merge sends zero FF', async () => {
  const observation = await change.observeRelayProgram('reentry-candidate-moved')

  assert.equal(observation.ffCalls, 0)
  assert.deepEqual(observation.ffPinnedCandidates, [])
  assert.equal(observation.facts.includes('Published'), false)
})
test('WHAT[change-integration-008] reentry with landed mismatch never records Published', async () => {
  const observation = await change.observeRelayProgram('reentry-landed-mismatch')

  assert.equal(observation.ffCalls, 1)
  assert.deepEqual(observation.ffPinnedCandidates, ['rebased-1'])
  assert.equal(observation.facts.includes('Published'), false)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { execFileSync, spawnSync } = await import("node:child_process");
const { mkdtempSync, rmSync, writeFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const change = await import('../../../dist/Change/Surface.js');

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

integrationTest('WHAT[change-integration-008] Adapter_ffMerge_clean_fast_forward_returns_exact_candidate_receipt', async () => {
  const fx = fixture()

  try {
    const result = await change.gitFfMerge(fx.gitAdapter, fx.candidate, 'main', fx.expectedHead, fx.candidateHead)

    assert.deepEqual(result, { ok: true, value: fx.candidateHead })
    assert.equal(git(fx.repo, 'symbolic-ref', '--short', 'HEAD'), 'main')
    assert.equal(git(fx.repo, 'rev-parse', 'refs/heads/main'), fx.candidateHead)
  } finally {
    fx.remove()
  }
})

integrationTest('WHAT[change-integration-008] Adapter_ffMerge_moved_head_fails_closed_without_advancing_head', async () => {
  const fx = fixture()

  try {
    const movedHead = commit(fx.repo, 'moved.txt', 'moved\n', 'move target')
    const result = await change.gitFfMerge(fx.gitAdapter, fx.candidate, 'main', fx.expectedHead, fx.candidateHead)

    assert.deepEqual(result, { ok: false, error: 'target ref moved' })
    assert.equal(git(fx.repo, 'rev-parse', 'refs/heads/main'), movedHead)
    assert.equal(git(fx.candidate, 'rev-parse', 'HEAD'), fx.candidateHead)
  } finally {
    fx.remove()
  }
})
}
