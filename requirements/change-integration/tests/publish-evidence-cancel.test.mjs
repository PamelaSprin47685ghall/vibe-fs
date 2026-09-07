import assert from 'node:assert/strict'
import test from 'node:test'

const change = await import('../../../dist/Change/Surface.js')

// ── PublicationEvidence & Single Authorized Publish Entrypoint (R12) ─────────

test('WHAT[CHGINT-001] fresh quality candidate runs to Published with verified publication evidence', async () => {
  const observation = await change.observeRelayProgram('fresh')

  assert.deepEqual(observation.verdict, { kind: 'Published', detail: 'rebased-1' })
  assert.deepEqual(observation.invalidations, ['InitialRebaseRequired'])
  assert.deepEqual(observation.continuations, ['surface-loop-1'])
  assert.equal(observation.ffGateHeld.length, 1)
  assert.equal(observation.ffGateHeld[0], true)
  assert.deepEqual(observation.ffExpectedHeads, ['target-1'])
  assert.equal(observation.gateAcquireCount, 1)
  assert.equal(observation.gateReleaseCount, 1)
})

test('WHAT[CHGINT-001] pre-rebase C1/S1 certificate cannot authorize rebased S2 candidate and fails closed', async () => {
  const observation = await change.observeRelayProgram('rebase-reuse-old-cert')

  assert.equal(observation.verdict.kind, 'IntegrationFailed')
  assert.match(observation.verdict.detail, /Live certificate snapshot does not match rebased evidence snapshot/)
  assert.deepEqual(observation.invalidations, ['InitialRebaseRequired'])
  assert.deepEqual(observation.continuations, ['surface-loop-1'])
  assert.equal(observation.ffCalls, 0)
  assert.equal(observation.gateAcquireCount, 0)
  assert.equal(observation.facts.includes('Published'), false)
})

test('WHAT[CHGINT-005] cleanup failure after landed FF preserves published fact as PublishedPendingCleanup', async () => {
  const observation = await change.observeRelayProgram('cleanup-failed')

  // In R12/R13, cleanup failure after FF succeeds must NEVER masquerade as IntegrationFailed
  // It must preserve the publication outcome and report PublishedPendingCleanup
  assert.equal(observation.verdict.kind, 'PublishedPendingCleanup')
  assert.match(observation.verdict.detail, /Published rebased-1/)
  assert.match(observation.verdict.detail, /cleanup pending:/)
  assert.match(observation.verdict.detail, /disk detached/)
  assert.ok(observation.facts.includes('Published'))
  assert.ok(!observation.facts.includes('JobFailed'))
})

// ── Cancellation Taxonomy (R13) ──────────────────────────────────────────────

test('WHAT[CHGINT-001] cancellation during manager loop returns Cancelled verdict and does not burn retry budget', async () => {
  const observation = await change.observeRelayProgram('cancelled-program')

  assert.deepEqual(observation.verdict, { kind: 'Cancelled', detail: 'cancelled' })
  assert.equal(observation.facts.includes('Published'), false)
  assert.equal(observation.facts.includes('JobFailed'), false)
  assert.equal(observation.gateAcquireCount, 0)
  assert.equal(observation.gateReleaseCount, 0)
  assert.equal(observation.ffCalls, 0)
  assert.deepEqual(observation.ffPinnedCandidates, [])
  assert.deepEqual(observation.ffExpectedHeads, [])
  assert.deepEqual(observation.invalidations, [])
  assert.deepEqual(observation.continuations, [])
})

// ── Reentry Publication Evidence Verification & CAS Fail-Closed (R12) ─────────

test('WHAT[CHGINT-007] classifyPublishClaim three-way reality ordering', () => {
  assert.equal(change.classifyPublishClaim('r1', 'r1', 'h1').kind, 'AlreadyFastForwarded')
  assert.equal(change.classifyPublishClaim('h1', 'r1', 'h1').kind, 'PublishReady')
  assert.equal(change.classifyPublishClaim('h9', 'r1', 'h1').kind, 'ClaimExpired')
  assert.equal(change.classifyPublishClaim(null, 'r1', 'h1').kind, 'HeadUnreadable')
})

test('WHAT[CHGINT-013] CAS miss releases gate and invalidates certificate without publishing', async () => {
  const observation = await change.observeRelayProgram('cas-miss')

  assert.deepEqual(observation.ffGateHeld, [true])
  assert.equal(observation.facts.includes('Published'), false)
  assert.deepEqual(observation.invalidations, ['PublishCasMissed'])
  assert.deepEqual(observation.continuations, ['surface-loop-1'])
  assert.equal(observation.gateHeldAfterRun, false)
})

test('WHAT[CHGINT-014] stale certificate fails closed and never enters publish gate', async () => {
  const observation = await change.observeRelayProgram('stale-certificate')

  assert.deepEqual(observation.invalidations, ['WorkspaceChangedAfterAssessment'])
  assert.equal(observation.gateAcquireCount, 0)
  assert.equal(observation.facts.includes('Published'), false)
})
// ── Reentry with Complete Durable Facts: Pinned Physical FF (R12) ─────────────
// Physical doubles record every FF call exactly (expected head + pinned
// candidate); they never read source files or copy the model. At most one
// physical FF is allowed per run; the merge itself runs on the pin.

test('WHAT[CHGINT-003] reentry with valid complete claim reenters without CandidateReady and publishes on the pin', async () => {
  const observation = await change.observeRelayProgram('reentry-valid')

  assert.deepEqual(observation.verdict, { kind: 'Published', detail: 'rebased-1' })
  assert.deepEqual(observation.ffExpectedHeads, ['target-1'])
  assert.deepEqual(observation.ffPinnedCandidates, ['rebased-1'])
  assert.equal(observation.ffCalls, 1)
  assert.ok(observation.ffCalls <= 1)
  assert.equal(observation.ffGateHeld.length, 1)
  assert.equal(observation.ffGateHeld[0], true)
  assert.ok(observation.facts.includes('Published'))
})

test('WHAT[CHGINT-014] reentry with wrong original-vs-rebased snapshot sends zero FF', async () => {
  const observation = await change.observeRelayProgram('reentry-wrong-snapshot')

  assert.equal(observation.ffCalls, 0)
  assert.deepEqual(observation.ffPinnedCandidates, [])
  assert.equal(observation.facts.includes('Published'), false)
})

test('WHAT[CHGINT-003] reentry missing RebasedCandidateReady evidence sends zero FF', async () => {
  const observation = await change.observeRelayProgram('reentry-missing-rebased')

  assert.equal(observation.ffCalls, 0)
  assert.deepEqual(observation.ffPinnedCandidates, [])
  assert.equal(observation.facts.includes('Published'), false)
  assert.equal(observation.verdict.kind, 'IntegrationFailed')
})

test('WHAT[CHGINT-003] reentry with conflicting claim snapshot sends zero FF', async () => {
  const observation = await change.observeRelayProgram('reentry-claim-snapshot-mismatch')

  assert.equal(observation.ffCalls, 0)
  assert.deepEqual(observation.ffPinnedCandidates, [])
  assert.equal(observation.facts.includes('Published'), false)
  // Expired claim re-enters the manager loop and consumes the terminal signal.
  assert.deepEqual(observation.verdict, { kind: 'IntegrationFailed', detail: 'scenario-complete' })
  assert.equal(observation.signalCount, 1)
})

test('WHAT[CHGINT-003] reentry with conflicting claim target sends zero FF', async () => {
  const observation = await change.observeRelayProgram('reentry-claim-target-mismatch')

  assert.equal(observation.ffCalls, 0)
  assert.deepEqual(observation.ffPinnedCandidates, [])
  assert.equal(observation.facts.includes('Published'), false)
  assert.equal(observation.verdict.kind, 'IntegrationFailed')
  // Hard target mismatch refuses publish recovery without entering the loop.
  assert.equal(observation.signalCount, 0)
})

test('WHAT[CHGINT-003] old incomplete claim decode fails closed in fold and recordFact', () => {
  const incomplete = change.fact('PublishClaimed', { rebasedCommit: 'r1', expectedHead: 'h1' })
  assert.throws(() => change.recordFact(change.empty(), 'job_1', incomplete), /Incomplete PublishClaimed payload/)

  const foldResult = change.fold([
    {
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
    },
    {
      kind: 'RebasedCandidateReady',
      payload: {
        jobId: 'job_1',
        rebasedCommit: 'r1',
        targetHeadSnapshot: 'h1',
        workspaceSnapshotId: 'snapshot-rebased-1',
      },
    },
    {
      kind: 'PublishClaimed',
      payload: { jobId: 'job_1', rebasedCommit: 'r1', expectedHead: 'h1' },
    },
  ])
  assert.equal(foldResult.ok, false)
  assert.match(foldResult.error, /Incomplete PublishClaimed payload/)
})

test('WHAT[CHGINT-008] reentry with candidate moving between read and merge sends zero FF', async () => {
  const observation = await change.observeRelayProgram('reentry-candidate-moved')

  assert.equal(observation.ffCalls, 0)
  assert.deepEqual(observation.ffPinnedCandidates, [])
  assert.equal(observation.facts.includes('Published'), false)
})

test('WHAT[CHGINT-008] reentry with landed mismatch never records Published', async () => {
  const observation = await change.observeRelayProgram('reentry-landed-mismatch')

  assert.equal(observation.ffCalls, 1)
  assert.deepEqual(observation.ffPinnedCandidates, ['rebased-1'])
  assert.equal(observation.facts.includes('Published'), false)
})

test('WHAT[CHGINT-001] reentry gate cancellation returns Cancelled with zero FF', async () => {
  const observation = await change.observeRelayProgram('reentry-gate-cancelled')

  assert.deepEqual(observation.verdict, { kind: 'Cancelled', detail: 'cancelled' })
  assert.equal(observation.ffCalls, 0)
  assert.deepEqual(observation.ffPinnedCandidates, [])
  assert.equal(observation.gateAcquireCount, 0)
  assert.equal(observation.gateReleaseCount, 0)
  assert.equal(observation.facts.includes('Published'), false)
})

test('WHAT[CHGINT-003] reentry FF-success with Published-append failure never masquerades as success', async () => {
  const observation = await change.observeRelayProgram('reentry-published-append-failed')

  assert.equal(observation.ffCalls, 1)
  assert.deepEqual(observation.ffPinnedCandidates, ['rebased-1'])
  assert.equal(observation.facts.includes('Published'), false)
  assert.equal(observation.verdict.kind, 'IntegrationFailed')
})

test('WHAT[CHGINT-005] reentry cleanup failure preserves PublishedPendingCleanup', async () => {
  const observation = await change.observeRelayProgram('reentry-cleanup-failed')

  assert.equal(observation.verdict.kind, 'PublishedPendingCleanup')
  assert.match(observation.verdict.detail, /Published rebased-1/)
  assert.match(observation.verdict.detail, /cleanup pending:/)
  assert.match(observation.verdict.detail, /disk detached/)
  assert.equal(observation.ffCalls, 1)
  assert.deepEqual(observation.ffPinnedCandidates, ['rebased-1'])
  assert.ok(observation.facts.includes('Published'))
})

test('WHAT[CHGINT-007] published-but-unsettled reentry never replays FF', async () => {
  const observation = await change.observeRelayProgram('reentry-published-unsettled')

  assert.equal(observation.ffCalls, 0)
  assert.deepEqual(observation.ffPinnedCandidates, [])
  assert.deepEqual(observation.ffExpectedHeads, [])
})

test('WHAT[CHGINT-013] superseding rebased evidence appends while the old claim cannot reenter after supersession', () => {
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

test('WHAT[CHGINT-013] recordFact keeps the latest rebased record instead of the first write', () => {
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

test('WHAT[CHGINT-001] old certificate still fails closed after the rebased record supersedes it', async () => {
  const observation = await change.observeRelayProgram('rebase-reuse-old-cert')

  assert.deepEqual(observation.facts, ['CandidateReady', 'RebasedCandidateReady'])
  assert.equal(observation.verdict.kind, 'IntegrationFailed')
  assert.match(observation.verdict.detail, /Live certificate snapshot does not match rebased evidence snapshot/)
  assert.equal(observation.ffCalls, 0)
  assert.equal(observation.facts.includes('Published'), false)
})

test('WHAT[CHGINT-013] stale claim superseded by newer rebased evidence re-enters the loop without FF', async () => {
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
