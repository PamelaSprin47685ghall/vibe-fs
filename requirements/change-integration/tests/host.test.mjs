// CHGINT-002/006/009/011 — Host-facing consequences stay plain.

import assert from 'node:assert/strict'
import test from 'node:test'

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

test('WHAT[CHGINT-002] HOST_sweep_failure_aborts_engine_initialization', async () => {
  const runner = (command) => command.args[0] === 'worktree' ? Promise.resolve([128, '', 'no .git']) : Promise.resolve([0, '', ''])
  const result = await change.gitListWorktrees(change.createGit('/repo', runner))
  assert.equal(result.ok, false)
  assert.match(result.error, /no \.git/)
})

test('WHAT[CHGINT-002] HOST_ForkManagerJob_surfaces_the_engine_verdict_error', async () => {
  const runner = () => Promise.resolve([0, ' M dirty.fs\n', ''])
  assert.equal(await change.gitIsDirty(change.createGit('/repo', runner), '/tmp/hostfw5'), true)
})

test('WHAT[CHGINT-009] manager loop keeps the durable job worktree', () => {
  let projection = change.createJob(change.empty(), job('hostfw8', '/tmp/wt-hostfw8'))
  projection = change.recordFact(projection, 'hostfw8', change.fact('CandidateReady', {
    candidateCommit: 'c1',
    workspaceSnapshotId: 'snapshot-1',
    qualityCertificateId: 'certificate-1',
  }))
  const continued = change.find(projection, 'hostfw8')
  assert.equal(continued.worktreePath, '/tmp/wt-hostfw8')
  assert.deepEqual(continued.facts, ['CandidateReady'])
})

test('WHAT[CHGINT-011] HOST_JoinPublishedAvailable_engine_init_failure_is_an_error_result', async () => {
  const result = await change.gitFreezeTargetBranch(change.createGit('/repo', () => Promise.resolve([128, '', 'bad repo'])))
  assert.equal(result.ok, false)
  assert.match(result.error, /bad repo/)
})

test('WHAT[CHGINT-006] HOST_awaitManager_stages_the_worktree_after_a_completed_manager_run', async () => {
  const runner = (command) => command.args[0] === 'worktree' ? Promise.resolve([0, '', '']) : Promise.resolve([0, '', ''])
  const resource = await change.worktreeCreate(change.createGit('/repo', runner), 'hostfw10', '/tmp/hostfw10')
  assert.equal(resource.ok, true)
  assert.equal(change.worktreePath(resource.value), '/tmp/hostfw10')
  await change.worktreeDispose(resource.value)
})
