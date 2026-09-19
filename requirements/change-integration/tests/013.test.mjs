import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");

const change = await import('../../../dist/Change/Surface.js')

test('WHAT[change-integration-013] target movement before publish invalidates the certificate and continues the loop without entering the gate', async () => {
  const observation = await change.observeRelayProgram('target-moved')

  assert.deepEqual(observation.invalidations, ['TargetAdvanced'])
  assert.deepEqual(observation.continuations, ['surface-loop-1'])
  assert.deepEqual(observation.rebaseGateHeld, [false])
  assert.deepEqual(observation.ffGateHeld, [])
  assert.equal(observation.gateAcquireCount, 0)
  assert.equal(observation.facts.includes('Published'), false)
})
test('WHAT[change-integration-013] CAS miss invalidates certificate rebases and continues the loop after releasing the gate', async () => {
  const observation = await change.observeRelayProgram('cas-miss')

  assert.deepEqual(observation.ffGateHeld, [true])
  assert.deepEqual(observation.ffExpectedHeads, ['target-1'])
  assert.equal(observation.gateAcquireCount, 1)
  assert.equal(observation.gateReleaseCount, 1)
  assert.deepEqual(observation.invalidations, ['PublishCasMissed'])
  assert.deepEqual(observation.continuations, ['surface-loop-1'])
  assert.deepEqual(observation.rebaseGateHeld, [false])
  assert.equal(observation.facts.includes('Published'), false)

  const release = observation.timeline.indexOf('gate:release')
  const invalidate = observation.timeline.indexOf('invalidate:PublishCasMissed')
  const rebase = observation.timeline.indexOf('git:rebase')
  const continuation = observation.timeline.indexOf('continue:surface-loop-1')
  assert.ok(release < invalidate && invalidate < rebase && rebase < continuation)
})
test('WHAT[change-integration-013] CAS miss appends the complete claim then a superseding rebased record, continues once, publishes at most once', async () => {
  const observation = await change.observeRelayProgram('cas-miss')

  assert.deepEqual(observation.facts, ['PublishClaimed', 'RebasedCandidateReady'])
  assert.deepEqual(observation.invalidations, ['PublishCasMissed'])
  assert.deepEqual(observation.continuations, ['surface-loop-1'])
  assert.ok(observation.ffCalls <= 1)
  assert.equal(observation.facts.includes('Published'), false)
  assert.equal(observation.gateAcquireCount, 1)
  assert.equal(observation.gateReleaseCount, 1)
})
test('WHAT[change-integration-013] target movement appends a superseding rebased record and continues once without publishing', async () => {
  const observation = await change.observeRelayProgram('target-moved')

  assert.deepEqual(observation.facts, ['CandidateReady', 'RebasedCandidateReady'])
  assert.deepEqual(observation.invalidations, ['TargetAdvanced'])
  assert.deepEqual(observation.continuations, ['surface-loop-1'])
  assert.equal(observation.facts.includes('Published'), false)
  assert.equal(observation.gateAcquireCount, 0)
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

test('WHAT[change-integration-013] target_movement_makes_the_rebased_binding_stale', () => {
  assert.equal(classifyRebased('h2').kind, 'NeedsRebase')
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

test('WHAT[change-integration-013] THEOREM_stale_target_invalidates_the_rebased_binding', () => {
  const folded = foldEvents([createEvent(JOB_A, 'ses_orch_a'), candidateEvent(JOB_A), rebasedEvent(JOB_A)])
  assert.deepEqual(factsOf(folded, JOB_A), ['CandidateReady', 'RebasedCandidateReady'])
  assert.equal(classifyRebased('h1').kind, 'PublishReady')
  assert.equal(classifyRebased('h2').kind, 'NeedsRebase')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");

const change = await import('../../../dist/Change/Surface.js')

test('WHAT[change-integration-013] CAS miss releases gate and invalidates certificate without publishing', async () => {
  const observation = await change.observeRelayProgram('cas-miss')

  assert.deepEqual(observation.ffGateHeld, [true])
  assert.equal(observation.facts.includes('Published'), false)
  assert.deepEqual(observation.invalidations, ['PublishCasMissed'])
  assert.deepEqual(observation.continuations, ['surface-loop-1'])
  assert.equal(observation.gateHeldAfterRun, false)
})
test('WHAT[change-integration-013] superseding rebased evidence appends while the old claim cannot reenter after supersession', () => {
  const created = {
    kind: 'ManagerJobCreated',
    payload: {
      jobId: 'job_1',
      managerSessionId: 'ses_1',
      managerAgent: 'manager',
      byname: 'Road',
      worktreeIdentity: 'wt_1',
      worktreePath: '/tmp/wt1',
      targetRef: 'refs/heads/main',
      targetBranchFrozen: 'refs/heads/main',
    },
  }
  const rebased = (rebasedCommit, expectedHead, snapshot) => ({
    kind: 'RebasedCandidateReady',
    payload: { jobId: 'job_1', rebasedCommit, targetHeadSnapshot: expectedHead, workspaceSnapshotId: snapshot },
  })
  const claim = (rebasedCommit, expectedHead, snapshot) => ({
    kind: 'PublishClaimed',
    payload: {
      jobId: 'job_1',
      targetRef: 'refs/heads/main',
      rebasedCommit,
      expectedHead,
      workspaceSnapshotId: snapshot,
      qualityCertificateId: 'certificate-1',
      authorityRevision: 'authority-1',
    },
  })

  const superseded = change.fold([created, rebased('r1', 'h1', 'snapshot-rebased-1'), rebased('r2', 'h2', 'snapshot-rebased-2')])
  assert.equal(superseded.ok, true)

  const staleClaim = change.fold([
    created,
    rebased('r1', 'h1', 'snapshot-rebased-1'),
    rebased('r2', 'h2', 'snapshot-rebased-2'),
    claim('r1', 'h1', 'snapshot-rebased-1'),
  ])
  assert.equal(staleClaim.ok, false)
  assert.match(staleClaim.error, /does not match admitted rebased commit/)

  const retryClaim = change.fold([
    created,
    rebased('r1', 'h1', 'snapshot-rebased-1'),
    rebased('r2', 'h2', 'snapshot-rebased-2'),
    claim('r2', 'h1', 'snapshot-rebased-1'),
    claim('r2', 'h2', 'snapshot-rebased-2'),
  ])
  assert.equal(retryClaim.ok, true)
})
test('WHAT[change-integration-013] recordFact keeps the latest rebased record instead of the first write', () => {
  const job = 'job_1'
  let projection = change.empty()
  projection = change.createJob(projection, {
    jobId: job,
    managerSessionId: 'ses_1',
    managerAgent: 'manager',
    byname: 'Road',
    worktreeIdentity: 'wt_1',
    worktreePath: '/tmp/wt1',
    targetRef: 'refs/heads/main',
    targetBranchFrozen: 'refs/heads/main',
  })
  const rebased = (rebasedCommit, expectedHead, snapshot) =>
    change.fact('RebasedCandidateReady', { rebasedCommit, targetHeadSnapshot: expectedHead, workspaceSnapshotId: snapshot })
  projection = change.recordFact(projection, job, rebased('r1', 'h1', 'snapshot-rebased-1'))
  projection = change.recordFact(projection, job, rebased('r2', 'h2', 'snapshot-rebased-2'))
  assert.deepEqual(change.find(projection, job).facts, ['RebasedCandidateReady'])
})
test('WHAT[change-integration-013] stale claim superseded by newer rebased evidence re-enters the loop without FF', async () => {
  const observation = await change.observeRelayProgram('reentry-stale-claim-superseded')

  // The CAS-missed R1 claim beside the superseding R2 record is expired: the
  // run continues the manager loop (one continuation, both signals consumed)
  // instead of failing closed or fast-forwarding the old pin.
  assert.deepEqual(observation.continuations, ['surface-loop-1'])
  assert.deepEqual(observation.verdict, { kind: 'IntegrationFailed', detail: 'scenario-complete' })
  assert.equal(observation.signalCount, 2)
  assert.equal(observation.ffCalls, 0)
  assert.deepEqual(observation.ffPinnedCandidates, [])
  assert.deepEqual(observation.ffExpectedHeads, [])
  assert.deepEqual(observation.facts, [])
  assert.equal(observation.gateAcquireCount, 0)
  assert.equal(observation.facts.includes('Published'), false)
})
}
