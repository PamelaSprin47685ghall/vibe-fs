import assert from 'node:assert/strict'
import test from 'node:test'

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

test('WHAT[CHGINT-017] multi-road confluence requires post-integration verification and preserves certificate invalidation', () => {
  const jobA = 'job_a'
  const jobB = 'job_b'
  let state = change.empty()

  const initA = change.createJobResult(state, payload({ jobId: jobA, worktreeIdentity: 'wt_a' }))
  assert.equal(initA.ok, true)
  const initB = change.createJobResult(initA.state, payload({ jobId: jobB, worktreeIdentity: 'wt_b' }))
  assert.equal(initB.ok, true)

  // Both roads pass independently in their own worktrees
  const candA = change.applyFact(
    initB.state,
    change.fact('CandidateReady', {
      candidateCommit: 'commit_a',
      workspaceSnapshotId: 'snap_a',
      qualityCertificateId: 'cert_a',
    }),
  )
  assert.equal(candA.ok, true)

  const candB = change.applyFact(
    candA.state,
    change.fact('CandidateReady', {
      candidateCommit: 'commit_b',
      workspaceSnapshotId: 'snap_b',
      qualityCertificateId: 'cert_b',
    }),
  )
  assert.equal(candB.ok, true)

  // When Road A publishes first, target advances to commit_a
  const claimedA = change.applyFact(candB.state, change.fact('PublishClaimed', { jobId: jobA, expectedHead: 'root', targetCommit: 'commit_a' }))
  assert.equal(claimedA.ok, true)
  const pubA = change.applyFact(claimedA.state, change.fact('Published', { publishedCommit: 'commit_a' }))
  assert.equal(pubA.ok, true)

  // Road B's certificate is invalidated due to target head advance and rebase requirement
  const rebaseB = change.applyFact(
    pubA.state,
    change.fact('CertificateInvalidated', {
      jobId: jobB,
      workspaceSnapshotId: 'snap_b_stale',
      reason: 'TargetAdvanced',
    }),
  )
  assert.equal(rebaseB.ok, true)
  assert.equal(change.jobView(rebaseB.state, jobB).status, 'Auditing')
})
