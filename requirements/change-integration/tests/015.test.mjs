import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const change = await import("../../../dist/Change/Surface.js");

const published = (jobId, head) => ({ kind: 'Published', jobId, head })

test('WHAT[change-integration-015] VERDICT_MAILBOX_pending_interrupt_stays_interrupted_then_next_publish_delivers_exactly_once', async () => {
  const mailbox = change.createVerdictMailbox()
  change.verdictMailboxStartJob(mailbox)
  const interrupt = change.createVerdictInterrupt()
  const pending = change.verdictMailboxJoinAvailable(mailbox, 8, interrupt)
  change.fireVerdictInterrupt(interrupt, 'UserMessageArrived')
  const out = await pending
  assert.equal(out.kind, 'Interrupted')
  assert.equal(out.reason, 'UserMessageArrived')
  assert.notEqual(out.kind, 'ResultsAvailable', 'interrupt must never masquerade as a batch')

  // Interrupting the wait leaves the job active. Its next waiter must wake,
  // rather than the removed waiter absorbing this job's eventual publication.
  const next = change.verdictMailboxJoinAvailable(mailbox, 8, change.createVerdictInterrupt())
  change.verdictMailboxPublish(mailbox, published('job-2', 'head-2'))
  const after = await next
  assert.equal(after.kind, 'ResultsAvailable')
  assert.equal(after.count, 1)
  assert.equal(after.verdicts[0].kind, 'Published')
  assert.equal(after.verdicts[0].detail, 'head-2')
  assert.equal(change.verdictMailboxPendingCount(mailbox), 0)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");

const change = await import('../../../dist/Change/Surface.js')
const JOB = 'job_reverify'
const MANAGER = 'ses_mgr'
const payload = (overrides = {}) => ({
  jobId: JOB,
  managerSessionId: MANAGER,
  managerAgent: 'manager',
  byname: 'Road',
  worktreeIdentity: 'wt_reverify',
  worktreePath: '/tmp/wt_reverify',
  targetRef: 'refs/heads/main',
  targetBranchFrozen: 'refs/heads/main',
  ...overrides,
})

test('WHAT[change-integration-015] worktree mutation during repair invalidates previous certificate and forces re-verification', () => {
  const initial = change.createJobResult(change.empty(), payload())
  assert.equal(initial.ok, true)

  // Candidate produced on snapshot-1 with certificate-1
  const candidate1 = change.applyFact(
    initial.state,
    change.fact('CandidateReady', {
      candidateCommit: 'c1',
      workspaceSnapshotId: 'snapshot-1',
      qualityCertificateId: 'cert-1',
    }),
  )
  assert.equal(candidate1.ok, true)
  assert.equal(change.jobView(candidate1.state, JOB).status, 'CandidateReady')

  // When DevOps repairs mutate the worktree, snapshot advances to snapshot-2, invalidating cert-1
  const invalidated = change.applyFact(
    candidate1.state,
    change.fact('CertificateInvalidated', {
      workspaceSnapshotId: 'snapshot-2',
      reason: 'WorkspaceChangedAfterAssessment',
    }),
  )
  assert.equal(invalidated.ok, true)
  assert.equal(change.jobView(invalidated.state, JOB).status, 'Auditing')

  // Publishing cannot proceed until new candidate with valid certificate on snapshot-2 is presented
  const invalidPublish = change.applyFact(
    invalidated.state,
    change.fact('PublishClaimed', {
      expectedHead: 'c1',
      targetCommit: 'c1',
    }),
  )
  assert.equal(invalidPublish.ok, false)
})
}
