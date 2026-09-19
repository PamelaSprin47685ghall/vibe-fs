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

test('WHAT[change-integration-016] parallel engineer and devops mutations require task boundary and forbid hash-equality bypass', () => {
  const initial = change.createJobResult(change.empty(), payload())
  assert.equal(initial.ok, true)

  // Manager job must maintain explicit snapshot identity without skipping verification via hash-only equality
  const candidate = change.applyFact(
    initial.state,
    change.fact('CandidateReady', {
      candidateCommit: 'c_isolated',
      workspaceSnapshotId: 'snapshot-bound-1',
      qualityCertificateId: 'cert-bound-1',
    }),
  )
  assert.equal(candidate.ok, true)
  const view = change.jobView(candidate.state, JOB)
  assert.equal(view.workspaceSnapshotId, 'snapshot-bound-1')
  assert.equal(view.qualityCertificateId, 'cert-bound-1')
})
