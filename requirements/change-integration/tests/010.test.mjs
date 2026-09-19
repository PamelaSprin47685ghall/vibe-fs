import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");

const change = await import('../../../dist/Change/Surface.js')

test('WHAT[change-integration-010] rebase work holds the gate only for the ff mutation', async () => {
  const observation = await change.observeRelayProgram('fresh')

  assert.deepEqual(observation.rebaseGateHeld, [false])
  assert.deepEqual(observation.ffGateHeld, [true])
  assert.deepEqual(observation.ffExpectedHeads, ['target-1'])
  assert.equal(observation.gateAcquireCount, 1)
  assert.equal(observation.gateReleaseCount, 1)
  assert.equal(observation.gateHeldAfterRun, false)
})
test('WHAT[change-integration-010] conflict resolution never acquires the publish gate', async () => {
  const observation = await change.observeRelayProgram('rebase-conflict')

  assert.deepEqual(observation.rebaseGateHeld, [false])
  assert.deepEqual(observation.ffGateHeld, [])
  assert.equal(observation.gateAcquireCount, 0)
  assert.equal(observation.gateHeldAfterRun, false)
})
test('WHAT[change-integration-010] 10,000 Continue signals complete the real manager loop with exact effects and balanced resources', async () => {
  const observation = await change.observeManagerLoopBurst(10000)

  assert.deepEqual(observation.verdict, { kind: 'IntegrationFailed', detail: 'burst-complete' })
  assert.equal(observation.continuationCount, 10000)
  assert.equal(observation.signalCount, 10001)
  assert.equal(observation.gateAcquireCount, 0)
  assert.equal(observation.gateReleaseCount, 0)
  assert.equal(observation.gateHeldAfterRun, false)
  assert.equal(observation.factCount, 0)
  assert.equal(observation.gitCallCount, 0)
  assert.equal(observation.continuations, undefined)
  assert.equal(observation.timeline, undefined)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");

const change = await import('../../../dist/Change/Surface.js')
const JOB = 'job_1'
const MANAGER = 'ses_m'
const payload = (overrides = {}) => ({
  jobId: JOB,
  managerSessionId: MANAGER,
  managerAgent: 'manager',
  byname: 'Road',
  worktreeIdentity: 'wt_1',
  worktreePath: '/tmp/wt1',
  targetRef: 'refs/heads/main',
  targetBranchFrozen: 'refs/heads/main',
  ...overrides,
})
const created = (overrides) => change.createJob(change.empty(), payload(overrides))
const fact = {
  candidateReady: (candidateCommit = 'c1', workspaceSnapshotId = 'snapshot-1', qualityCertificateId = 'certificate-1') =>
    change.fact('CandidateReady', { candidateCommit, workspaceSnapshotId, qualityCertificateId }),
  conflictDetected: (conflictFiles = ['a.fs', 'b.fs']) =>
    change.fact('ConflictDetected', {
      candidateCommit: 'c1',
      targetHeadSnapshot: 'h1',
      workspaceSnapshotId: 'snapshot-conflict',
      conflictFiles,
      diagnosticsDigest: 'digest',
    }),
  rebased: (targetHeadSnapshot = 'h1', workspaceSnapshotId = 'snapshot-rebased') =>
    change.fact('RebasedCandidateReady', {
      rebasedCommit: 'r1',
      targetHeadSnapshot,
      workspaceSnapshotId,
    }),
  publishClaimed: (
    workspaceSnapshotId = 'snapshot-rebased',
    qualityCertificateId = 'certificate-1',
    authorityRevision = 'authority-1',
  ) =>
    change.fact('PublishClaimed', {
      targetRef: 'refs/heads/main',
      rebasedCommit: 'r1',
      expectedHead: 'h1',
      workspaceSnapshotId,
      qualityCertificateId,
      authorityRevision,
    }),
  published: () => change.fact('Published', { candidateCommit: 'c1', resultingTargetHead: 'r1' }),
  failed: (reason = 'boom') => change.fact('JobFailed', { reason }),
  abandoned: () => change.fact('JobAbandoned', null),
}
const jobAt = (value, projection = created()) => {
  const next = change.recordFact(projection, JOB, value)
  return { projection: next, job: change.find(next, JOB) }
}
const classifyRebased = (head, rebasedCommit = 'r1', snapshot = 'h1') =>
  change.classifyRebasedCandidate(head ?? null, rebasedCommit, snapshot)
const classifyClaim = (head, rebasedCommit = 'r1', expectedHead = 'h1') =>
  change.classifyPublishClaim(head ?? null, rebasedCommit, expectedHead)
const createdEvent = {
  kind: 'ManagerJobCreated',
  payload: payload(),
}
const candidateEvent = {
  kind: 'CandidateReady',
  payload: {
    jobId: JOB,
    candidateCommit: 'c1',
    workspaceSnapshotId: 'snapshot-1',
    qualityCertificateId: 'certificate-1',
  },
}
const publishedEvent = {
  kind: 'Published',
  payload: { jobId: JOB, candidateCommit: 'c1', resultingTargetHead: 'r1' },
}
const foldProjection = (events) => {
  const result = change.fold(events)
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.value
}
const worktreeRequested = { kind: 'WorktreeCreateRequested', payload: { jobId: JOB, worktreeIdentity: 'manager/job_1', worktreePath: '/tmp/wt1' } }
const worktreeCreated = { kind: 'WorktreeCreated', payload: { jobId: JOB, worktreeIdentity: 'manager/job_1', worktreePath: '/tmp/wt1' } }

test('WHAT[change-integration-010] ORCH_005_a_rebased_candidate_publishes_only_while_the_target_has_not_moved', () => {
  assert.equal(classifyRebased('h1').kind, 'PublishReady')
  assert.equal(classifyRebased('h2').kind, 'NeedsRebase')
})
}
