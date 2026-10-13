import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");

const change = await import('../../../dist/Change/Surface.js')

test('WHAT[change-integration-001] fresh quality candidate runs the full publish lifecycle to Published', async () => {
  const observation = await change.observeRelayProgram('fresh')

  assert.deepEqual(observation.verdict, { kind: 'Published', detail: 'rebased-1' })
  assert.deepEqual(observation.continuations, ['surface-loop-1'])

  // No invalidation stream remains. The surviving order is the rebase record,
  // the next tenure's review signal, then the claim and the gated ff.
  assert.deepEqual(observation.timeline, [
    'await:Candidate',
    'fact:CandidateReady',
    'git:rebase',
    'git:read-head:rebased-1',
    'fact:RebasedCandidateReady',
    'continue:surface-loop-1',
    'await:Candidate',
    'gate:acquire',
    'git:read-head:rebased-1',
    'fact:PublishClaimed',
    'git:ff:rebased-1',
    'fact:Published',
    'gate:release',
    'relay:terminate',
  ])
})
test('WHAT[change-integration-001] retirement continues the loop without publishing', async () => {
  const observation = await change.observeRelayProgram('retired')

  assert.deepEqual(observation.continuations, ['surface-loop-1'])
  assert.equal(observation.timeline[0], 'await:Continue')
  assert.equal(observation.timeline[1], 'continue:surface-loop-1')
  assert.equal(observation.timeline[2], 'await:ExceptionalTerminal')
  assert.equal(observation.ffCalls, 0)
  assert.equal(observation.facts.includes('Published'), false)
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
  ...overrides})
const created = (overrides) => change.createJob(change.empty(), payload(overrides))
const fact = {
  candidateReady: (candidateCommit = 'c1') =>
    change.fact('CandidateReady', { candidateCommit }),
  conflictDetected: (conflictFiles = ['a.fs', 'b.fs']) =>
    change.fact('ConflictDetected', {
      candidateCommit: 'c1',
      targetHeadSnapshot: 'h1',
      conflictFiles,
      diagnosticsDigest: 'digest'}),
  rebased: (targetHeadSnapshot = 'h1') =>
    change.fact('RebasedCandidateReady', {
      rebasedCommit: 'r1',
      targetHeadSnapshot}),
  publishClaimed: (
    authorityRevision = 'authority-1') =>
    change.fact('PublishClaimed', {
      targetRef: 'refs/heads/main',
      rebasedCommit: 'r1',
      expectedHead: 'h1',
      authorityRevision}),
  published: () => change.fact('Published', { candidateCommit: 'c1', resultingTargetHead: 'r1' }),
  failed: (reason = 'boom') => change.fact('JobFailed', { reason }),
  abandoned: () => change.fact('JobAbandoned', null)}
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
  payload: payload()}
const candidateEvent = {
  kind: 'CandidateReady',
  payload: {
    jobId: JOB,
    candidateCommit: 'c1'}}
const publishedEvent = {
  kind: 'Published',
  payload: { jobId: JOB, candidateCommit: 'c1', resultingTargetHead: 'r1' }}
const foldProjection = (events) => {
  const result = change.fold(events)
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.value
}
const worktreeRequested = { kind: 'WorktreeCreateRequested', payload: { jobId: JOB, worktreeIdentity: 'manager/job_1', worktreePath: '/tmp/wt1' } }
const worktreeCreated = { kind: 'WorktreeCreated', payload: { jobId: JOB, worktreeIdentity: 'manager/job_1', worktreePath: '/tmp/wt1' } }

test('WHAT[change-integration-001] ORCH_003_a_created_job_persists_the_manager_agent_and_the_worktree_identity', () => {
  const job = change.find(created(), JOB)
  assert.deepEqual(job, {
    jobId: 'job_1',
    managerSessionId: 'ses_m',
    managerAgent: 'manager',
    byname: 'Road',
    worktreeIdentity: 'wt_1',
    worktreePath: '/tmp/wt1',
    targetRef: 'refs/heads/main',
    targetBranchFrozen: 'refs/heads/main',
    facts: []})
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");

const change = await import('../../../dist/Change/Surface.js')

test('WHAT[change-integration-001] fresh quality candidate runs to Published with verified publication evidence', async () => {
  const observation = await change.observeRelayProgram('fresh')

  assert.deepEqual(observation.verdict, { kind: 'Published', detail: 'rebased-1' })
  assert.deepEqual(observation.continuations, ['surface-loop-1'])
  assert.equal(observation.ffGateHeld.length, 1)
  assert.equal(observation.ffGateHeld[0], true)
  assert.deepEqual(observation.ffExpectedHeads, ['target-1'])
  assert.equal(observation.gateAcquireCount, 1)
  assert.equal(observation.gateReleaseCount, 1)
})
test('WHAT[change-integration-001] a candidate whose commit no longer matches the rebased evidence pin fails closed', async () => {
  // Publication evidence is the durable Reviewed plus the work's authority
  // count. The surviving fail-closed check is the pin: a live candidate that
  // no longer equals the admitted rebased commit is refused with zero writes.
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
      targetBranchFrozen: 'refs/heads/main'}}
  const events = (claimCommit) => [
    created,
    { kind: 'RebasedCandidateReady', payload: { jobId: 'job_1', rebasedCommit: 'rebased-2', targetHeadSnapshot: 'target-1' } },
    {
      kind: 'PublishClaimed',
      payload: {
        jobId: 'job_1',
        targetRef: 'refs/heads/main',
        rebasedCommit: claimCommit,
        expectedHead: 'target-1',
        authorityRevision: 'authority-1'}},
  ]

  const mismatched = change.fold(events('rebased-1'))
  assert.equal(mismatched.ok, false)
  assert.match(mismatched.error, /does not match admitted rebased commit/)

  const matching = change.fold(events('rebased-2'))
  assert.equal(matching.ok, true, matching.ok ? '' : matching.error)
})
test('WHAT[change-integration-001] cancellation during manager loop returns Cancelled verdict and does not burn retry budget', async () => {
  const observation = await change.observeRelayProgram('cancelled-program')

  assert.deepEqual(observation.verdict, { kind: 'Cancelled', detail: 'cancelled' })
  assert.equal(observation.facts.includes('Published'), false)
  assert.equal(observation.facts.includes('JobFailed'), false)
  assert.equal(observation.gateAcquireCount, 0)
  assert.equal(observation.gateReleaseCount, 0)
  assert.equal(observation.ffCalls, 0)
  assert.deepEqual(observation.ffPinnedCandidates, [])
  assert.deepEqual(observation.ffExpectedHeads, [])
  assert.deepEqual(observation.continuations, [])
})
test('WHAT[change-integration-001] reentry gate cancellation returns Cancelled with zero FF', async () => {
  const observation = await change.observeRelayProgram('reentry-gate-cancelled')

  assert.deepEqual(observation.verdict, { kind: 'Cancelled', detail: 'cancelled' })
  assert.equal(observation.ffCalls, 0)
  assert.deepEqual(observation.ffPinnedCandidates, [])
  assert.equal(observation.gateAcquireCount, 0)
  assert.equal(observation.gateReleaseCount, 0)
  assert.equal(observation.facts.includes('Published'), false)
})
test('WHAT[change-integration-001] superseded rebased evidence still fails closed when a later claim cites the old pin', async () => {
  const observation = await change.observeRelayProgram('reentry-stale-claim-superseded')

  // The later rebased record supersedes the claimed pin. Reentry treats the
  // old claim as expired evidence and continues the loop instead of
  // fast-forwarding it.
  assert.deepEqual(observation.continuations, ['surface-loop-1'])
  assert.equal(observation.ffCalls, 0)
  assert.deepEqual(observation.ffPinnedCandidates, [])
  assert.equal(observation.facts.includes('Published'), false)
  assert.equal(observation.gateAcquireCount, 0)
})
}
